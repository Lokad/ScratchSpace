using System;

namespace Lokad.ScratchSpace.Helpers
{
    /// <summary>
    ///     A list implementation that supports appending from a thread while 
    ///     reading from another thread. 
    /// </summary>
    public sealed class AppendList<T>
    {
        /// <summary> The array that backs the list. </summary>
        private T[] _backing = Array.Empty<T>();

        /// <summary> The number of elements in the list. </summary>
        public int Count { get; private set; }

        /// <summary> Append an element to the list. </summary>
        /// <remarks> 
        ///     Not re-entrant. However, reads to the list may be done from
        ///     other threads while this method is executing. 
        /// </remarks>
        public void Append(T elem)
        {
            if (Count == _backing.Length)
            {
                var newBacking = new T[Math.Max(4, _backing.Length * 2)];

                _backing.AsSpan().CopyTo(newBacking);
                _backing = newBacking;
            }

            _backing[Count++] = elem;
        }

        /// <summary> Access the i-th element of the list. </summary>
        /// <remarks>
        ///     Reads done at the same time as an <see cref="Append"/> work as expected,
        ///     returning the value that was present in the backing.
        ///     
        ///     Since this returns a reference, it is possible to assign values back to 
        ///     the cell. However, keep in mind that: 
        ///     
        ///       - Assigning from multiple threads causes the typical data race
        ///         problems.  
        ///       - Assignments done at the same time as an <see cref="Append"/> may be
        ///         undone. 
        /// </remarks>
        public ref T this[int i] => ref _backing[i];
    }
}
