using Lokad.ScratchSpace.Files;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Lokad.ScratchSpace.Tests.Files
{
    public sealed class disk_file_source
    {
        [InlineData("0000.bin", true)]
        [InlineData("9999.bin", true)]
        [InlineData("000A.bin", false)]
        [InlineData("00000.bin", false)]
        [InlineData("-000.bin", false)]
        [InlineData("0000.dat", false)]
        [Theory]
        public void matches_naming_scheme(string filename, bool match)
        {
            var matches = DiskFileSource.MatchesNamingScheme(filename);
            if (match) Assert.True(matches);
            else Assert.False(matches);
        }

        [InlineData(0, "A/0000.bin")]
        [InlineData(1, "A/0001.bin")]
        [InlineData(2, "A/0002.bin")]
        [InlineData(3, "A/0003.bin")]
        [Theory]
        public void full_file_path_1(int nth, string expect) =>
            Assert.Equal(Path.Combine(expect.Split("/")), DiskFileSource.FullFilePath(new[] { "A" }, nth, 10));

        [InlineData(0, "A/0000.bin")]
        [InlineData(1, "B/0000.bin")]
        [InlineData(2, "A/0001.bin")]
        [InlineData(3, "B/0001.bin")]
        [Theory]
        public void full_file_path_2(int nth, string expect) =>
            Assert.Equal(Path.Combine(expect.Split("/")), DiskFileSource.FullFilePath(new[] { "A", "B" }, nth, 10));

        [InlineData(0, "A/0000.bin")]
        [InlineData(1, "B/0000.bin")]
        [InlineData(2, "C/0000.bin")]
        [InlineData(3, "A/0001.bin")]
        [InlineData(4, "B/0001.bin")]
        [InlineData(5, "C/0001.bin")]
        [Theory]
        public void full_file_path_3(int nth, string expect) =>
            Assert.Equal(Path.Combine(expect.Split("/")), DiskFileSource.FullFilePath(new[] { "A", "B", "C" }, nth, 10));
    }
}
