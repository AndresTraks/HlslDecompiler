using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Tests;

// The checksum is recalled, not documented, so every container fxc wrote is
// what holds it to fxc's. About one in six has a last block with no room for
// the counts, so both of its branches run.
public class DxbcChecksumTests
{
    [Test]
    public void ReproducesTheChecksumOfEveryContainerFxcWrote()
    {
        List<string> containers = [.. Directory
            .EnumerateFiles("CompiledShaders", "*.fxc", SearchOption.AllDirectories)
            .Where(path => File.ReadAllBytes(path).AsSpan(0, 4).SequenceEqual("DXBC"u8))];
        List<string> wrong = [.. containers.Where(path =>
        {
            byte[] bytes = File.ReadAllBytes(path);
            return !DxbcChecksum.Compute(bytes)
                .SequenceEqual(bytes[DxbcChecksum.Offset..(DxbcChecksum.Offset + DxbcChecksum.Length)]);
        })];

        Assert.Multiple(() =>
        {
            Assert.That(containers, Has.Count.GreaterThan(400));
            Assert.That(containers.Count(path => (new FileInfo(path).Length - 20) % 64 >= 56),
                Is.GreaterThan(0), "a container whose counts need a block of their own");
            Assert.That(wrong, Is.Empty);
        });
    }
}
