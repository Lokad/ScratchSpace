using System.Threading;

namespace Lokad.ScratchSpace.Helpers
{
    /// <summary> Used for pinning and unpinning files. </summary>
    public sealed class Pinner
    {
        /// <summary>
        ///     If the 'unpinnable' bit is set, the pinner no longer supports 
        ///     pins. 
        /// </summary>
        /// <remarks>
        ///     We intentionally choose a small number because we do not 
        ///     want to allow too many pins (since once a pin is acquired, 
        ///     the acquirer's priority should be to finish whatever it 
        ///     needed the pinned resource for, and then unpinning), so 
        ///     accumulating pins is a sign that something is wrong.
        /// </remarks>
        const int Unpinnable = 1 << 10;

        /// <summary> The pin count and unpinnable flag. </summary>
        private int _pin;

        /// <summary> The current number of pins. </summary>
        public int PinCount => _pin % Unpinnable;

        /// <summary> Can the pinner be pinned ? </summary>
        /// <remarks>
        ///     An "unpinnable" pinner will never allow pinning again ; it is 
        ///     merely counting down unpins. 
        ///     
        ///     Even if not unpinnable, pin attempts may still fail because the 
        ///     maximum number of pins has been reached.
        /// </remarks>
        public bool IsUnpinnable => _pin >= Unpinnable;

        /// <summary>
        ///     If the pinner is currently pinnable, increments the pin count
        ///     and returns true. Otherwise, returns false.
        /// </summary>
        public bool TryPin()
        {
            var pin = _pin;
            while (true)
            {
                if (pin >= Unpinnable - 1)
                    // Do not attempt pin if the pinner is currently unpinnable, 
                    // or the maximum number of pins has been reached.
                    return false;

                var old = Interlocked.CompareExchange(ref _pin, pin + 1, pin);
                if (pin == old)
                    return true;

                pin = old;
            }
        }

        /// <summary> Makes the pinner unpinnable (if it wasn't already). </summary>
        /// <returns> True iff the pinner wasn't unpinnable and the pin count is zero. </returns>
        public bool MakeUnpinnable()
        {
            var pin = _pin;
            while (true)
            {
                if (pin >= Unpinnable)
                    // Nothing to do if the pinner is currently unpinnable.
                    return false;

                var old = Interlocked.CompareExchange(ref _pin, pin + Unpinnable, pin);
                if (pin == old)
                    return old == 0;

                pin = old;
            }
        }

        /// <summary> Decrements the pin count. </summary>
        /// <returns>
        ///     True iff the pinner is currently unpinnable and the pin count
        ///     has reached zero.
        /// </returns>
        public bool Unpin() => Interlocked.Decrement(ref _pin) == Unpinnable;
    }
}
