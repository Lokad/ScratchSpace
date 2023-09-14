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

            // Create directories if they do not exist yet, and remove any files which match 
            // the naming scheme but are not used by this instance (i.e. left over from 
            // a previous execution with a higher count). 
            var existing = new HashSet<string>();
            foreach (var f in _folders)
            {
                Directory.CreateDirectory(f);
                foreach (var filename in Directory.EnumerateFiles(f))
                {
                    if (!MatchesNamingScheme(filename)) continue;
                    existing.Add(Path.Combine(f, filename));
                }
            }

            for (var i = 0; i < Count; ++i)
                existing.Remove(FullFilePath(_folders, i, Count));

            foreach (var toRemove in existing)
            {
                try
                {
                    File.Delete(toRemove);
                }
                catch (Exception ex)
                {
                    new ArgumentException($"Cannot remove existing file {toRemove}.", nameof(folders), ex);
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<(int id, IFileMemory file)> ScanExistingFiles()
        {
            // TODO: at some point, include the DateTime of creation, so that
            // the caller can decide to drop the oldest files first.

            for (var i = 0; i < Count; ++i)
            {
                var file = new FileInfo(FullFilePath(_folders, i, Count));
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
            var path = FullFilePath(_folders, i, Count);

            var mmf = MemoryMappedFile.CreateFromFile(
                path, FileMode.Create, null, _fileSize);

            return new MemoryMapper(mmf, _fileSize);
        }

        /// <summary> The full path of a file. </summary>
        public static string FullFilePath(IReadOnlyList<string> folders, int i, int count)
        {
            if (i < 0 || i >= count)
                throw new ArgumentOutOfRangeException(
                    nameof(i),
                    $"No file {i}, max is {count}");

            return Path.Combine(
                folders[i % folders.Count],
                $"{i/folders.Count:D4}.bin");
        }

        /// <summary>
        ///     True if the provided filename matches the naming scheme used by the disk file 
        ///     source (and therefore, if not used, should be deleted).
        /// </summary>
        public static bool MatchesNamingScheme(string filename) =>
            filename.Length == 8 &&
            filename.EndsWith(".bin") &&
            char.IsDigit(filename[0]) &&
            char.IsDigit(filename[1]) &&
            char.IsDigit(filename[2]) &&
            char.IsDigit(filename[3]);

        /// <inheritdoc/>
        public int Count => _folders.Count * _filesPerFolder;
    }
}
