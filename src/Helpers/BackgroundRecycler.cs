using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Lokad.ScratchSpace.Helpers
{
    /// <summary>
    ///     Used to send values of type <typeparamref name="T"/> to a background
    ///     thread to be recycled ; used to return brand new values.
    /// </summary>
    /// <remarks>
    ///     The interface is separated into "user-side" which is a multi-threaded
    ///     environment that uses values and requests them to be recycled, and 
    ///     "recycler-side" which is a single-threaded environment that receives
    ///     the values-to-be-recycled. 
    /// </remarks>
    public sealed class BackgroundRecycler<T> where T : class
    {
        /// <summary> User-side ; the current available value. </summary>
        /// <remarks>
        ///     Calling this method will block the thread if there is currently
        ///     no available value.
        ///     
        ///     This is thread-safe, but it means that it's possible for a value
        ///     to be returned by this method right before another thread passes
        ///     it to <see cref="RequestRecycle"/>. Therefore, the value should
        ///     support being marked as "to be recycled" so that there is no 
        ///     risk: 
        ///     
        ///      - Thread A calls GetCurrent(), receives Obj
        ///      - Thread A decides Obj should be recycled, marks it as TBR
        ///      - Thread B calls GetCurrent(), also receives Obj
        ///      - Thread A calls RequestRecycle(Obj)
        ///      - Thread C (the recycling thread) recycles Obj
        ///      - Thread B attempts to use Obj, but since it was marked as 
        ///        TBR, nothing happens.
        /// </remarks>
        public T GetCurrent()
        {
            var c = _userSideCurrent;
            if (c != null) return c;

            lock (_syncRoot)
            {
                // Try again inside lock, since maybe another thread
                // was in the lock and changed _userSideCurrent before
                // returning.
                c = _userSideCurrent;
                if (c != null) return c;

                // Block until the recycling thread can provide 
                // a value.
                return _userSideCurrent = _recyclerToUser.Take();
            }
        }

        /// <summary>
        ///     User-side ; if <paramref name="current"/> is still the 
        ///     current value, causes it to be sent to recycling. From 
        ///     then on, <see cref="GetCurrent"/> will return another value.
        /// </summary>
        public void RequestRecycle(T current)
        {
            var old = Interlocked.CompareExchange(ref _userSideCurrent, null, current);
            
            if (ReferenceEquals(old, current))
                // We were the ones to remove it!
                _userToRecycler.Add(current);
        }

        /// <summary>
        ///     If <see cref="GetCurrent"/> would return immediately,
        ///     returns the value that would have been returned.
        /// </summary>
        /// <remarks> 
        ///     Never blocks. Can be called from both user-side and 
        ///     recycling-side.
        /// </remarks>
        public T CurrentIfExists => _userSideCurrent;

        /// <summary>
        ///     The current value on the user side. 
        ///     Becomes null when recycled.
        /// </summary>
        private T _userSideCurrent;

        /// <summary>
        ///     Blocking queue used to return recycled values back to the 
        ///     user side.
        /// </summary>
        private readonly BlockingCollection<T> _recyclerToUser =
            new BlockingCollection<T>();

        /// <summary>
        ///     Blocking queue used to send values-to-be-recycled to the
        ///     recycling side.
        /// </summary>
        private readonly BlockingCollection<T> _userToRecycler =
            new BlockingCollection<T>();

        /// <summary>
        ///     Syncronization to protect <see cref="_userSideCurrent"/>
        ///     and <see cref="_recyclerToUser"/>.
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        ///     Recycler-side ; wait for a value-to-be-recycled to be
        ///     sent to the recycling thread (through <see cref="RequestRecycle"/>).
        ///     Returns false if the timeout is exceeded or cancellation is 
        ///     requested.
        /// </summary>
        public bool TryNextToBeRecycled(
            TimeSpan wait,
            CancellationToken cancel,
            out T toBeRecycled)
        =>
            _userToRecycler.TryTake(out toBeRecycled, (int)wait.TotalMilliseconds, cancel);

        /// <summary>
        ///     Recycler-side ; send a recycled value back to the user-side.
        /// </summary>
        public void CompleteRecycle(T wasRecycled) =>
            _recyclerToUser.Add(wasRecycled);
    }
}
