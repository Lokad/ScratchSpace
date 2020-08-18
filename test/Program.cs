using Lokad.ScratchSpace.Blocks;

namespace Lokad.ScratchSpace.Tests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var folders = args.Length == 0 ? new[] { "C:\\LokadData\\scratch" } : args;
            Benchmark.Benchmark.Run(
                folders, 
                115 * 4, 
                BlockAddress.MaxFileSize);
        }
    }
}
