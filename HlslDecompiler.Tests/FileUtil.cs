using System.IO;

namespace HlslDecompiler.Tests;

public static class FileUtil
{
    public static void MakeFolder(string hlslOutputFilename)
    {
        // Unconditional: CreateDirectory does nothing where the folder is there
        // already, and the Exists test in front of it was two tests racing to make
        // the same one.
        Directory.CreateDirectory(Path.GetDirectoryName(hlslOutputFilename));
    }
}
