using Lokad.ContentAddr;
using Lokad.ScratchSpace.Blocks;
using Lokad.ScratchSpace.Files;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Lokad.ScratchSpace.Tests.Benchmark
{
    public static class Benchmark
    {
        public static void Run(string[] folders, int files, long size)
        {
            var scratch = new Scratch(
                new DiskFileSource(folders, files / folders.Length, size),
                CancellationToken.None);

            Console.WriteLine(
                "Found {0} values in existing files",
                scratch.Count);

            var pool = ArrayPool<int>.Create();

            const int Length = 1 << 24;

            var i = 0;

            var sw = Stopwatch.StartNew();

            Task.WaitAll(Enumerable.Range(0, 8).Select(_ => Task.Run(Act)).ToArray());

            void Act()
            {
                var available = new List<Hash>();
                var left = new int[Length];
                var right = new int[Length];
                var rand = new Random(100);

                void LoadRandomInto(int[] array)
                {
                    while (true)
                    {
                        if (available.Count == 0)
                        {
                            for (var s = 0; s < Length; ++s)
                                array[s] = rand.Next();

                            return;
                        }

                        var pos = rand.Next(available.Count);
                        var hash = available[pos];

                        try
                        {
                            scratch.Read(1, hash, span =>
                            {
                                if (!BlockHasher.ComputeHash(span).Equals(hash))
                                    Console.WriteLine("Bad hash!");

                                MemoryMarshal.Cast<byte, int>(span).CopyTo(array);
                                return true;
                            });

                            return;
                        }
                        catch (MissingBlockException)
                        {
                            Console.WriteLine("{0}  Missing {1}", sw.Elapsed, hash);
                            available[pos] = available[available.Count - 1];
                            available.RemoveAt(available.Count - 1);
                        }
                    }
                }

                int j = 0;
                while (j < 10000)
                {
                    j = Interlocked.Increment(ref i);

                    var result = pool.Rent(Length);

                    LoadRandomInto(left);
                    LoadRandomInto(right);

                    for (var c = 0; c < Length; ++c)
                        result[c] = left[c] + right[c];

                    var hash = BlockHasher.ComputeHash(
                        MemoryMarshal.Cast<int, byte>(result.AsSpan(0, Length)));

                    available.Add(hash);
                    scratch.Write(1, hash, Length * sizeof(int), span =>
                    {
                        MemoryMarshal.Cast<int, byte>(result.AsSpan(0, Length)).CopyTo(span);
                        pool.Return(result);
                    });

                    var total = j * sizeof(int) * (long)Length;
                    Console.WriteLine(
                        "i = {3}  {0}  Wrote {1} MB ({2:F2} MBps)",
                        sw.Elapsed.TotalSeconds,
                        total >> 20,
                        total / sw.Elapsed.TotalSeconds / 1024 / 1024,
                        j);
                }
            }
        }
    }
}