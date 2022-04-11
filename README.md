This library is used when RAM is not enough, and it's necessary to spill data to disk. Once disk space runs out, the library automatically clears up old data to make room for newer data.

Data is saved as independent binary blocks, which are stored in files on the disk (or disks). The files are memory-mapped, allowing the OS page cache to keep frequently accessed (or recently written) blocks in-memory.

If necessary, blocks can be kept in up to `1 << 24` completely separate sub-stores (called _realms_). The intent is to support multi-user configurations where each user has its own separate realm. If not needed, just use a realm of `0` everywhere. 

A few numbers/limitations: 

 - Blocks should be large (they are padded to 4096 bytes, so smaller blocks will waste space),
   but not too large (up to `int.MaxValue - 32` bytes per block, ~2GB). 
 - Up to 1021 on-disk files can be used, and each file can contain up to 16GB. Total storage available is 1021 * 16 = 15.95 TB.
 - Up to 16777216 blocks may be stored across all files (this limit can be reached if blocks are smaller than 1MB on average).
 - The block index consumes 448MB of memory (as a single array, allocated at creation time).

## Usage

To use, create a `DiskFileSource`, specifying: 

 - The folders where data files should be written (this allows writing files to multiple disks in round-robin fashion).
 - The number of files per folder (the total number of files, maximum is `BlockAddress.MaxFileCount`).
 - The size of each file (maximum is `BlockAddress.MaxFileSize`)

Then, create a `Scratch` from the disk-file-source. The class will automatically 
reload data from files written previously and still present in the folder(s). 

```c#
// 16GB * 10 files * 2 folders = 320GB total
DiskFileSource src = new DiskFileSource(
	folders: new[]{ "/mnt/nvme0", "/mnt/nvme1" },
	filesPerFolder: 10,
	fileSize: 16 << 30); 

// Cancellation token is used to stop background threads
Scratch scratch = new Scratch(src, CancellationToken.None);
```

If the folders contain files from a previous execution, the library will spawn a background thread to attempt to reload them and see if any of the blocks inside can be reused.

### High-level interface

Writing data consists in three steps: 

```c#
// User identifier (to keep data separated per-user)
uint userId = 1337u;

// 1. Allocate a writer from the Scratch object, representing the transaction
BlittableWriter w = scratch.Write(userId);

// 2. Write data to the writer
w.Write("Hello");
w.Write(2);
w.Write(new[]{ true, false }); 

// 3. Commit the data back to the Scratch object
Hash hash = w.Commit();
```

The writer supports writing strings, values of unmanaged types, or 
arrays (or `Memory<T>`) of unmanaged types. The resulting hash can then be used to query the scratch space:

```c#
(string, bool[]) value = scratch.Read(userId, hash, BlittableReader r =>
{
	string str = r.ReadString();
	int length = r.Read<int>();
	bool[] array = r.Read<bool>(length);

	return (str, array);
});
```

If the block no longer exists, a `MissingBlockException` will be thrown instead.

**Important**: arrays should never be modified after being passed to a writer, even after the writer has been committed. The scratch space takes ownership of the array and needs it to remain unchanged until it has been written to disk (and there is no safe way for client-side code to know when that happens). 

**Important**: the `BlittableReader` is a `ref struct` with mutable state. If you are not familiar with the behavior of these, please follow these two rules: 
 
 1. If you assign the reader to another variable, do not use the previous variable any longer.
 2. If you pass the reader as an argument to a function, do not use the previous variable any longer.

#### Performance tips

Try to minimize the number of calls to `BlittableWriter.Write`, and to write arrays or large strings instead of primitives or small strings. In an ideal situation, a 2GB block should be written with only 2 or 3 calls to `BlittableWriter.Write`.

The callback passed to `Scratch.Read` pins the underlying block until it returns. This can prevent the entire file from being recycled, which will lead to `Commit()` blocking once the library runs out of space. Because of that, the callback should run as quickly as possible. 

### Low-level interface

Save a new block of data by calling `scratch.Write(realm, hash, size, writer)` where:

 - `realm` is used to have up to `1 << 24` per-user sub-stores.
 - `hash` is the hash of the data to be saved, computed with `BlockHasher`. Currently, this uses SpookyHash, for its performance and lack of collisions.
 - `size` is the size of the data to be saved, in bytes. May not exceed `int.MaxValue - 32`.
 - `writer` is a function that will receive a `Span<byte>` of exactly `size` bytes. The function should write to that span a sequence of bytes that has the expected `hash`. 

**Important**: the `writer` will not be called immediately, instead it may take a few seconds. As such, if the function needs some resources (such as arrays of data) in order to function, these resources should remain available and unchanged until the function is called. 

The function returns nothing. Instead, the data that was just written to the scratch space can be read back with `scratch.Read(realm, hash, reader)`, where: 

 - `realm` and `hash` should be the same values originally provided.
 - `reader` is a function that will receive a `ReadOnlySpan<byte>` (the same that was written by the writer) and will return a value of type `T`.

The function `scratch.Read` will return the value returned by `reader`, throw the exception thrown by `reader`, or throw `MissingBlockException` if no block with the requested realm and hash was found. 

## Architecture (internal details)

The system is cut into three major sections: 

 - The `BlockIndex` is a dictionary that maps `(realm, hash)` pairs to block addresses represented by the `BlockAddress` type. To the index, the address type is fully opaque (beyond being a 32-bit value type with a `None` value to indicate its absence). 
 - The `FileWheel` is a storage system that keeps a bag of memory-mapped files open for reading and writing ; it can retrieve data at a given `BlockAddress`, and create a `BlockAddress` and schedule for data to be written there.
 - The `DiskFileSource` is responsible for creating memory-mapped files on the disk. Once the maximum file count is reached, it deletes the oldest file and creates a new one.

### The Block Index

This is a high-performance dictionary optimized for this specific use case. It supports multi-threaded lock-free reading, under the assumption that the number of reads is significantly greater than the number of writes. It can contain up to 16 million entries, and consumes a constant 448MB of space as two objects (to reduce GC pressure). 

### The File Wheel

This is a *service*: it has a background thread responsible for flushing written data to disk, and a public API that supports multi-threaded requests to read a block or allocate and write to a new block. 

The file wheel is currently optimized for burst allocations: sudden, short-lived spikes in allocation rate, followed by periods of low allocation rate during which the background thread can flush out its backlog. On a single NVMe disk (on a [Standard_L8s_v2](https://docs.microsoft.com/en-us/azure/virtual-machines/lsv2-series)), this was measured as allowing an average write rate of 600MBps, tolerating a spike rate of 1400MBps for a few seconds. When the burst exceeds the capacity of the system, write requests begin to block the calling thread ; read requests continue to be served.

