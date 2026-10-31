using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HlslDecompiler.Tests;

/// <summary>
/// The HLSL compiler the tests recompile with. On Windows it is d3dcompiler_47.dll
/// called in-process, the library fxc itself is a front end to; off Windows it is
/// fxc.exe under wine (from PATH, with the inherited WINEPREFIX), because wine's
/// own d3dcompiler is a different compiler.
/// </summary>
public static class ShaderCompiler
{
    // Flags1 for fxc /Op.
    public const uint NoPreshader = 1 << 8;

    // D3D_COMPILE_STANDARD_FILE_INCLUDE, as fxc resolves #include.
    private static readonly IntPtr StandardFileInclude = 1;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int D3DCompileFunction(
        byte[] source, nuint sourceSize,
        [MarshalAs(UnmanagedType.LPStr)] string sourceName,
        IntPtr defines, IntPtr include,
        [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
        [MarshalAs(UnmanagedType.LPStr)] string target,
        uint flags1, uint flags2, out IntPtr code, out IntPtr errors);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int D3DDisassembleFunction(
        byte[] source, nuint sourceSize, uint flags, IntPtr comments, out IntPtr disassembly);

    // ID3DBlob's vtable: IUnknown's QueryInterface, AddRef and Release, then GetBufferPointer and GetBufferSize.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFunction(IntPtr blob);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufferPointerFunction(IntPtr blob);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nuint GetBufferSizeFunction(IntPtr blob);

    private sealed class Library(IntPtr handle)
    {
        public readonly D3DCompileFunction Compile =
            Marshal.GetDelegateForFunctionPointer<D3DCompileFunction>(NativeLibrary.GetExport(handle, "D3DCompile"));
        public readonly D3DDisassembleFunction Disassemble =
            Marshal.GetDelegateForFunctionPointer<D3DDisassembleFunction>(NativeLibrary.GetExport(handle, "D3DDisassemble"));
    }

    private static readonly Lazy<Library> D3DCompiler = new(LoadD3DCompiler);

    private static readonly Lazy<string> Fxc = new(FindFxc);

    /// <summary>fxc.exe for wine, or null on Windows.</summary>
    public static string FxcPath => OperatingSystem.IsWindows() ? null : Fxc.Value;

    public static bool IsAvailable => OperatingSystem.IsWindows() ? D3DCompiler.Value != null : Fxc.Value != null;

    public const string UnavailableMessage =
        "No HLSL compiler: d3dcompiler_47.dll did not load, or off Windows FXC_EXE names no fxc.exe.";

    /// <summary>fxc /T profile [/E entryPoint] source /Fo objectFilename.</summary>
    /// <returns>The diagnostics, or null on success.</returns>
    public static string Compile(string profile, string entryPoint, string sourceFilename, string objectFilename,
        uint flags = 0)
    {
        if (!OperatingSystem.IsWindows())
        {
            return CompileWithFxc(profile, entryPoint, sourceFilename, objectFilename, flags);
        }

        byte[] source = File.ReadAllBytes(sourceFilename);
        int result = D3DCompiler.Value.Compile(source, (nuint)source.Length, sourceFilename, IntPtr.Zero,
            StandardFileInclude, entryPoint, profile, flags, 0, out IntPtr code, out IntPtr errors);
        try
        {
            if (result < 0 || code == IntPtr.Zero)
            {
                string diagnostics = errors != IntPtr.Zero ? ReadBlobText(errors) : null;
                return string.IsNullOrWhiteSpace(diagnostics) ? $"D3DCompile failed: 0x{result:X8}" : diagnostics;
            }
            File.WriteAllBytes(objectFilename, ReadBlob(code));
            return null;
        }
        finally
        {
            Release(code);
            Release(errors);
        }
    }

    /// <summary>fxc /dumpbin's listing of a compiled shader.</summary>
    public static string Disassemble(string binaryFilename)
    {
        if (!OperatingSystem.IsWindows())
        {
            return DisassembleWithFxc(binaryFilename);
        }

        byte[] binary = File.ReadAllBytes(binaryFilename);
        int result = D3DCompiler.Value.Disassemble(binary, (nuint)binary.Length, 0, IntPtr.Zero, out IntPtr listing);
        try
        {
            return result < 0 ? "" : ReadBlobText(listing);
        }
        finally
        {
            Release(listing);
        }
    }

    // The SDK's copy before the system's: the goldens were compiled with it, and
    // System32 carries a different build.
    private static Library LoadD3DCompiler()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string architecture = Environment.Is64BitProcess ? @"\x64\" : @"\x86\";
        string sdkCopy = SdkBinaries()
            .Select(path => Path.Combine(Path.GetDirectoryName(path), "d3dcompiler_47.dll"))
            .FirstOrDefault(path => path.Contains(architecture) && File.Exists(path));

        foreach (string candidate in new[] { Environment.GetEnvironmentVariable("D3DCOMPILER_DLL"), sdkCopy, "d3dcompiler_47.dll" })
        {
            if (candidate != null && NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return new Library(handle);
            }
        }
        return null;
    }

    private static byte[] ReadBlob(IntPtr blob)
    {
        IntPtr vtable = Marshal.ReadIntPtr(blob);
        IntPtr pointer = Marshal.GetDelegateForFunctionPointer<GetBufferPointerFunction>(
            Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size))(blob);
        nuint size = Marshal.GetDelegateForFunctionPointer<GetBufferSizeFunction>(
            Marshal.ReadIntPtr(vtable, 4 * IntPtr.Size))(blob);
        byte[] bytes = new byte[(int)size];
        Marshal.Copy(pointer, bytes, 0, bytes.Length);
        return bytes;
    }

    // Blobs of text end in a NUL.
    private static string ReadBlobText(IntPtr blob)
    {
        return System.Text.Encoding.UTF8.GetString(ReadBlob(blob)).TrimEnd('\0').Trim();
    }

    private static void Release(IntPtr blob)
    {
        if (blob != IntPtr.Zero)
        {
            Marshal.GetDelegateForFunctionPointer<ReleaseFunction>(
                Marshal.ReadIntPtr(Marshal.ReadIntPtr(blob), 2 * IntPtr.Size))(blob);
        }
    }

    private static string CompileWithFxc(string profile, string entryPoint, string sourceFilename,
        string objectFilename, uint flags)
    {
        var startInfo = CreateFxcProcessStartInfo();
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/T");
        startInfo.ArgumentList.Add(profile);
        if (entryPoint != null)
        {
            startInfo.ArgumentList.Add("/E");
            startInfo.ArgumentList.Add(entryPoint);
        }
        if ((flags & NoPreshader) != 0)
        {
            startInfo.ArgumentList.Add("/Op");
        }
        startInfo.ArgumentList.Add(AsFxcPath(sourceFilename));
        startInfo.ArgumentList.Add("/Fo");
        startInfo.ArgumentList.Add(AsFxcPath(objectFilename));

        using var process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? null : output.Trim();
    }

    private static string DisassembleWithFxc(string binaryFilename)
    {
        var startInfo = CreateFxcProcessStartInfo();
        startInfo.ArgumentList.Add("/nologo");
        startInfo.ArgumentList.Add("/dumpbin");
        startInfo.ArgumentList.Add(AsFxcPath(binaryFilename));

        using var process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static ProcessStartInfo CreateFxcProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo("wine")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Fxc.Value);
        return startInfo;
    }

    /// <summary>
    /// Wine does not translate arguments, so an absolute unix path reaches fxc as a
    /// `/` option and it exits 1 silently. Mapped through Z:, the unix root in a
    /// wine prefix. Every path handed to fxc must come through here.
    /// </summary>
    private static string AsFxcPath(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            return path;
        }
        return "Z:" + Path.GetFullPath(path).Replace('/', '\\');
    }

    // No SDK layout to search under wine; FXC_EXE names the copied fxc.exe.
    private static string FindFxc()
    {
        string fxcExe = Environment.GetEnvironmentVariable("FXC_EXE");
        return fxcExe != null && File.Exists(fxcExe) ? fxcExe : null;
    }

    // The SDKs' fxc.exe files, newest first.
    private static IOrderedEnumerable<string> SdkBinaries()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string[] roots =
        [
            Path.Combine(programFiles, "Windows Kits", "10", "bin"),
            Path.Combine(programFiles, "Windows Kits", "8.1", "bin"),
        ];
        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "fxc.exe", SearchOption.AllDirectories))
            .OrderByDescending(path => path);
    }
}
