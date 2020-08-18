using System.Threading;
using System.Threading.Tasks;
using Lokad.ScratchSpace.Helpers;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Helpers
{
    public sealed class append_list
    {
        [Fact]
        public void empty()
        {
            var l = new AppendList<int>();
            Assert.Equal(0, l.Count);
        }

        [Fact]
        public void one()
        {
            var l = new AppendList<int>();
            l.Append(10);

            Assert.Equal(1, l.Count);
            Assert.Equal(10, l[0]);
        }

        [Fact]
        public void two()
        {
            var l = new AppendList<int>();
            l.Append(10);
            l.Append(42);

            Assert.Equal(2, l.Count);
            Assert.Equal(10, l[0]);
            Assert.Equal(42, l[1]);
        }

        [Fact(Skip = "Smoke test; can take a while")]
        public async Task smoke_test()
        {
            // Append and read repeatedly, trying to cause an 
            // access violation.

            for (var c = 0; c < 1000; ++c)
            {
                var l = new AppendList<int>();

                var t1 = Task.Run(() =>
                {
                    for (var i = 0; i < 100000; ++i)
                        l.Append(i * 3);
                });

                var t2 = Task.Run(() =>
                {
                    for (var i = 0; i < 100000; ++i)
                    {
                        while (i >= l.Count)
                            Thread.Yield();

                        Assert.Equal(i * 3, l[i]);
                    }
                });

                await t1;
                await t2;
            }
        }
    }
}
