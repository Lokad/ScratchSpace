using Lokad.ScratchSpace.Mapping;
using System.Collections.Generic;

namespace Lokad.ScratchSpace.Files
{
    /// <summary>
    ///     Keeps track of <see cref="Count"/> files, each associated with a
    ///     numeric identifier. They can be deleted and re-created.
    /// </summary>
    /// <remarks>
    ///     This interface exists mostly to allow an in-memory 
    ///     implementation.
    /// </remarks>
    public interface IFileSource
    {
        /// <summary> Scan the disk for files that already exist, and return them. </summary>
        IEnumerable<(int id, IFileMemory file)> ScanExistingFiles();

        /// <summary>
        ///     Deletes the file with identifier 'i', creates a new empty file at
        ///     the same location, and memory-maps it.
        /// </summary>
        IFileMemory DeleteAndCreate(int id);

        /// <summary> The maximum number of files per folder. </summary>
        int Count { get; }
    }
}
