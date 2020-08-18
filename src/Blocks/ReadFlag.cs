using System;
using System.Threading;

namespace Lokad.ScratchSpace.Blocks
{
    /// <summary>
    ///     A lock placed on a <see cref="BlockHandle"/> to prevent access to that
    ///     block until its contents are ready to be read.
    /// </summary>
    /// <remarks>
    ///     Contents might be unavailable because 
    ///         1° they have not yet been written from the blittable-writer, or 
    ///         2° the block was recovered from an old file, and its hash has not 
    ///            been validated yet.
    ///     In either case, some additional work is needed to make the contents 
    ///     readable.
    /// </remarks>
    public struct ReadFlag
    {
        /// <summary> The lock. </summary>
        /// <remarks> If null, the read flag is unlocked. </remarks>
        private readonly Lazy<bool> _lock;

        /// <summary>
        ///     Waits for the block to be readable. If no other thread is currently 
        ///     working on making the block readable, it starts the processing to make
        ///     it readable.
        /// </summary>
        /// <remarks>
        ///     Will throw if the blob is not readable (e.g. incorrect hash).
        /// </remarks>
        /// <returns>
        ///     A new <see cref="ReadFlag"/> in the "readable" state, to be used to replace
        ///     the previous flag.
        /// </returns>
        public ReadFlag WaitUntilReadable()
        {
            _ = _lock?.Value;
            return default;
        }

        private ReadFlag(Lazy<bool> processing) =>
            _lock = processing ?? throw new ArgumentNullException(nameof(processing));

        /// <summary>
        ///     A flag that is triggered by the first call to 
        ///     <see cref="WaitUntilReadable"/>, and is released when the action 
        ///     returns. The action will be executed at most once.
        /// </summary>
        public static ReadFlag Triggered(Action a) =>
            new ReadFlag(new Lazy<bool>(
                () => { a(); return true; },
                LazyThreadSafetyMode.ExecutionAndPublication));

        /// <summary> A read flag that allows reading without any effort. </summary>
        public static readonly ReadFlag None = default;
    }
}
