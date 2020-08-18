using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Mapping;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Lokad.ScratchSpace.Files
{
    /// <summary>
    ///     Allocates memory-mapped files on the disk, each associated with an 
    ///     integer identifier between 0 and <see cref="Count"/> (minus one).
    /// </summary>
    /// <remarks>
    ///     Allows multiple folders, and multiple files per folder ; identifiers
    ///     are consecutive and are rotated through folders, i.e. file 0 is in 
    ///     folder 0, file 1 is in folder 1, and file N is in folder (N % FOLDERS).
    /// </remarks>
    public sealed class DiskFileSource : IFileSource
    {
        /// <summary> All folders in which files can be created. </summary>
        /// <remarks> 
        ///     At least one folder. Allocation traverses the folders in a 
        ///     round-robin fashion.
        /// </remarks>
        private readonly IReadOnlyList<string> _folders;

        /// <summary> The maximum number of files per folder. </summary>
        private readonly int _filesPerFolder;

        /// <summary> The size of each file. </summary>
        private readonly long _fileSize;

        public DiskFileSource(
            IReadOnlyList<string> folders, 
            int filesPerFolder,
            long fileSize)
        {
            _folders = folders ?? throw new ArgumentNullException(nameof(folders));
            if (folders.Count == 0)
                throw new ArgumentException("Must provide at least one folder",
                    nameof(folders));

            _filesPerFolder = filesPerFolder;
            if (Count < 3)
                throw new ArgumentException("Must have at least three files", 
                    nameof(filesPerFolder));

            if (Count > BlockAddress.MaxFileCount)
                throw new ArgumentException($"Must have at most {BlockAddress.MaxFileCount} files",
                    nameof(filesPerFolder));

            _fileSize = fileSize;
            if (fileSize < BlockAddress.BlockAlignment)
                throw new ArgumentException($"Files must contain at least {BlockAddress.BlockAlignment} bytes",
                    nameof(fileSize));

            if (fileSize > BlockAddress.MaxFileSize)
                throw new ArgumentException($"Files must contain at most {BlockAddress.MaxFileSize} bytes",
                    nameof(fileSize));

            foreach (var f in _folders)
                Directory.CreateDirectory(f);
        }

        /// <inheritdoc/>
        public IEnumerable<(int id, IFileMemory file)> ScanExistingFiles()
        {
            // TODO: at some point, include the DateTime of creation, so that
            // the caller can decide to drop the oldest files first.

            for (var i = 0; i < Count; ++i)
            {
                var file = new FileInfo(FullFilePath(i));
                if (!file.Exists) continue;

                if (file.Length != _fileSize)
                {
                    // Wrong file size, we don't want to bother with this.
                    file.Delete();
                    continue;
                }

                var mmf = MemoryMappedFile.CreateFromFile(file.FullName);
                var mmap = new MemoryMapper(mmf, _fileSize);

                yield return (i, mmap);
            }
        }

        /// <inheritdoc/>
        public IFileMemory DeleteAndCreate(int i)
        {
            var path = FullFilePath(i);

            var mmf = MemoryMappedFile.CreateFromFile(
                path, FileMode.Create, null, _fileSize);

            return new MemoryMapper(mmf, _fileSize);
        }

        /// <summary> The full path of a file. </summary>
        private string FullFilePath(int i)
        {
            if (i < 0 || i >= Count)
                throw new ArgumentOutOfRangeException(
                    nameof(i),
                    $"No file {i}, max is {Count}");

            return Path.Combine(
                _folders[i % _folders.Count],
                $"{i:D4}.bin");
        }

        /// <inheritdoc/>
        public int Count => _folders.Count * _filesPerFolder;
    }
}
