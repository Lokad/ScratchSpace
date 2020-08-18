using System;

namespace Lokad.ScratchSpace
{
    /// <summary> 
    ///     Thrown when a <see cref="BlittableReader"/> detects an
    ///     incorrect checksum while reading. 
    /// </summary>
    public sealed class CheckSumFailedException : Exception
    {
        public CheckSumFailedException()
        {
        }
    }
}
