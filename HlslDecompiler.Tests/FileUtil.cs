using System.IO;

namespace HlslDecompiler.Tests;

public static class FileUtil
{
    public static void MakeFolder(string hlslOutputFilename)
    {
        string directory = Path.GetDirectoryName(hlslOutputFilename);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
