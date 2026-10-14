using System.IO;

namespace HlslDecompiler.Tests;

public static class FileUtil
{
    public static void MakeFolder(string hlslOutputFilename)
    {
        // No Exists check: it races between parallel tests.
        Directory.CreateDirectory(Path.GetDirectoryName(hlslOutputFilename));
    }
}
