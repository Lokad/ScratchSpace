using Lokad.ContentAddr;
using System.Threading;
using System;
using Lokad.ScratchSpace.Files;
using Xunit;
using Lokad.ScratchSpace.Blocks;
using System.Diagnostics;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lokad.ScratchSpace.Tests.ScratchSpace
{
    /// <summary>
    /// 1. Use [Fact] during development and testing, otherwise restore [Fact(Skip)].
    /// 2. Remember, you should prepare the work folder <see cref="_workFolder"/> using the specialized test <see cref="prepare_work_folder">.
    /// 3. Do not forget to remove <see cref="_workFolder"/> once development is finished.
    /// </summary>
    public sealed class scratch_parallel
    {
        /// <summary>
        /// Working directory.
        /// </summary>
        private const string _workFolder = "C:\\LokadData\\scratch";

        /// <summary>
        /// Maximum number of files.
        /// </summary>
        private const int _fileNum = 115 * 4;

        /// <summary>
        /// Length of a block.
        /// </summary>
        private int _length = 1 << 17;

        /// <summary>
        /// Test block hash.
        /// </summary>
        private Hash _testHash = new Hash("9EADBAECD5DDC0641E42DD4FF5FDEC1F");

        /// <summary>
        /// User id.
        /// </summary>
        private const int realm = 1;

        /// <summary> 
        /// Waiting time of Scratch construction.
        /// Normally, 15s are enough to construct a Scratch object in order to pass tests.
        /// However, if it is not enough, for example, due to hardware problems, you can increase this value.  
        /// </summary>
        private const int _waitingTime = 1000;

        /// <summary>
        /// Block number which is used for testing.
        /// It is much inferior to <see cref="_blockCount">, 
        /// thus the test block will not be inserted in the last binary file and we can guarantee 
        /// that this block is written.
        /// </summary>
        private const int _testBlock = 6000;

        /// <summary>
        /// Number of written blocks.
        /// </summary>
        private const int _blockCount = 10000; 

        /// <summary>
        /// Generate a test block.
        /// </summary>
        /// <returns>Test block</returns>
        private int[] GetArray(int i)
        {
            var rand = new Random(i);

            var array = new int[_length];
            for (var c = 0; c < _length; ++c)
                array[c] = rand.Next();

            return array;
        }

        /// <summary>
        /// Manual test to launch only once before all tests.
        /// <see cref="_workFolder"/> should be manually cleaned after testing.
        /// </summary>
        [Fact(Skip = "Execute manually")]
        public void prepare_work_folder()
        {
            var files = 460;
            var scratch = new Scratch(
                new DiskFileSource(new[] { _workFolder }, files, BlockAddress.MaxFileSize/16),
                CancellationToken.None);

            for (var i = 0; i < _blockCount; ++i)
            {
                // generates a different array for every 'i'
                var array = GetArray(i); 

                var writer = scratch.Write(realm);
                writer.Write(array);
                var hash = writer.Commit();

                if (i == _testBlock)
                    Assert.Equal(hash, _testHash);
            }
        }

        /// <summary>
        /// Testing the possibility to write in and read from the scratch 
        /// while its constructor is still running.
        /// </summary>
        [Fact(Skip = "Execute manually")]
        public void construction_write_read()
        {
            using (var scratch = new Scratch(
                    new DiskFileSource(new[] { _workFolder }, _fileNum, BlockAddress.MaxFileSize/16),
                    CancellationToken.None))
            {
                var secLimit = TimeSpan.FromSeconds(1);
                var sw = Stopwatch.StartNew();

                Assert.Equal(0, scratch.Count);
                var elapsed = sw.Elapsed;

                Assert.True(elapsed < secLimit);

                // The object pre-construction does not need much time,
                // thus we can quickly pass to writing during construction.
                BlittableWriter w = scratch.Write(realm);

                w.Write("Hello");
                w.Write(2);
                w.Write(new[] { true, false });

                Hash hash = w.Commit();

                // Reading.
                (string, bool[]) value = scratch.Read(realm, hash, r =>
                {
                    string str = r.ReadString();
                    int length = r.Read<int>();
                    bool[] array = r.Read<bool>(length);

                    return (str, array);
                });
                Assert.Equal("Hello", value.Item1);
                Assert.Equal(new[] { true, false }, value.Item2);

                // Waiting for scratch construction.
                Thread.Sleep(_waitingTime);

                var writtenNum = 5938;
                Assert.Equal(writtenNum, scratch.Count);
            }
        }

        /// <summary>
        /// Testing whether a code block exists in binary files.
        /// </summary>
        [Fact(Skip = "Execute manually")]
        public void contains_written_hash()
        {
            // Load test block contents.
            using (var scratch = new Scratch(
                new DiskFileSource(new[] { _workFolder }, _fileNum, BlockAddress.MaxFileSize/16),
                CancellationToken.None))
            {
                // Waiting for scratch construction.
                Thread.Sleep(_waitingTime);
                Assert.True(scratch.ContainsKey(realm, _testHash));
            }
        }

        /// <summary>
        /// Testing whether multiple scratch object creations and readinds can break a written block.
        /// </summary>
        [Fact(Skip = "Execute manually")]
        public void reading_trials()
        {
            using (var scratch = new Scratch(
                    new DiskFileSource(new[] { _workFolder }, _fileNum, BlockAddress.MaxFileSize/16),
                    CancellationToken.None))
            {
                // Waiting for scratch construction.
                Thread.Sleep(2 * _waitingTime);

                var array = new int[_length];

                // Reading.
                scratch.Read(realm, _testHash, span =>
                {
                    if (!BlockHasher.ComputeHash(span).Equals(_testHash))
                        Console.WriteLine("Bad hash!");

                    MemoryMarshal.Cast<byte, int>(span).CopyTo(array);
                    return true;
                });

                Assert.Equal(GetArray(_testBlock), array);
            }
        }
    }
}
