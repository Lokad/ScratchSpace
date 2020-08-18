using Lokad.ScratchSpace.Helpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Helpers
{
    public sealed class pinner
    {
        [Fact]
        public void initial_count()
        {
            var p = new Pinner();
            Assert.Equal(0, p.PinCount);
            Assert.False(p.IsUnpinnable);
        }

        [Fact]
        public void pin_many()
        {
            var p = new Pinner();
            for (var i = 0; i < 1023; ++i)
            {
                Assert.Equal(i, p.PinCount);
                Assert.True(p.TryPin());
            }

            for (var i = 1023; i > 0; --i)
            {
                Assert.Equal(i, p.PinCount);
                Assert.False(p.Unpin());
            }

            Assert.Equal(0, p.PinCount);
        }

        [Fact]
        public void pin_too_many()
        {
            var p = new Pinner();
            for (var i = 0; i < 1023; ++i)
                Assert.True(p.TryPin());

            Assert.False(p.TryPin());
            Assert.Equal(1023, p.PinCount);
        }

        [Fact]
        public async Task pin_concurrent()
        {
            var p = new Pinner();
            await Task.WhenAll(
                Enumerable.Range(0, 1023).Select(_ => Task.Run(async () =>
                {
                    p.TryPin();
                    await Task.Delay(5);
                    p.Unpin();
                })));

            Assert.Equal(0, p.PinCount);
        }

        [Fact]
        public void cannot_pin_unpinnable()
        {
            var p = new Pinner();
            Assert.True(p.TryPin());
            Assert.False(p.MakeUnpinnable());

            Assert.Equal(1, p.PinCount);

            Assert.False(p.TryPin());
            Assert.True(p.Unpin());

            Assert.Equal(0, p.PinCount);
        }

        [Fact]
        public void making_no_pins_unpinnable_is_true()
        {
            var p = new Pinner();
            Assert.True(p.MakeUnpinnable());
        }

        [Fact]
        public async Task multi_threaded_unpinnable()
        {
            var p = new Pinner();
            p.TryPin();

            var loop = Task.Run(() =>
            {
                while (p.TryPin())
                    Assert.False(p.Unpin());
            });

            await Task.Delay(10);

            Assert.False(p.MakeUnpinnable());

            await loop;

            Assert.True(p.Unpin());
        }

    }
}
