using System;
using System.Collections.Generic;
using Lokad.ScratchSpace.Mapping;

namespace Lokad.ScratchSpace.Files
{
    /// <summary>
    ///     An in-memory implementation of <see cref="IFileSource"/>
    ///     for use in unit tests.
    /// </summary>
    public sealed class MemoryFileSource : IFileSource, IDisposable
    {
        private readonly int _fileSize;

        private readonly VolatileMemory[] _files;

        public MemoryFileSource(int fileCount, int fileSize)
        {
            _fileSize = fileSize;
            _files = new VolatileMemory[fileCount];
        }

        public int Count => _files.Length;

        /// <inheritdoc/>
        public IFileMemory DeleteAndCreate(int id)
        {
            if (_files[id] == null)
                _files[id] = new VolatileMemory(_fileSize);
            else
                _files[id].Clear();

            return _files[id];
        }

        /// <inheritdoc/>
        public IEnumerable<(int id, IFileMemory file)> ScanExistingFiles() =>
            Array.Empty<(int, IFileMemory)>();

        public void Dispose() { }
    }
}
