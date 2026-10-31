# Builds XnaXbox.exe into tools\xna-xbox\bin from XNA Game Studio 3.1's
# installer, XNAGS31_setup.exe (https://www.microsoft.com/download/details.aspx?id=39).
# Nothing is installed: the installer is unpacked with 7-Zip and an administrative
# msiexec, and the runtime files it needs are copied beside the exe. The XNA
# license allows one copy per device, so bin\ is not committed.
#
#   powershell -File tools\xna-xbox\build.ps1 -Installer C:\path\XNAGS31_setup.exe
param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [string]$SevenZip = "$env:ProgramFiles\7-Zip\7z.exe"
)
$ErrorActionPreference = 'Stop'

# The helper's source is kept here rather than in a .cs file, which the main
# project would pick up and fail to compile without XNA. It is written to a
# working folder outside the repository, with the unpacked installer.
$source = @'
// Compiles and assembles Xbox 360 shaders with XNA Game Studio 3.1's
// ShaderCompiler, the only Xenos compiler available outside the XDK.
//
//   XnaXbox compile <vs_3_0|ps_3_0> <source.fx> <output.bin>
//   XnaXbox assemble <source.asm> <output.bin>
//   XnaXbox listing <source.asm>
//
// Diagnostics go to standard error. listing writes the validator's listing of
// the assembled program to standard output: the validator prints the program
// only with a warning, which two trailing cnops guarantee (Xenia's
// shader-playground does the same), so its program is not the source's.
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

static class XnaXbox
{
    static int Main(string[] args)
    {
        try
        {
            if (args.Length == 4 && args[0] == "compile")
            {
                return Compile(args[1], args[2], args[3]);
            }
            if (args.Length == 3 && args[0] == "assemble")
            {
                return Assemble(File.ReadAllText(args[1]), args[2], false);
            }
            if (args.Length == 2 && args[0] == "listing")
            {
                return Assemble(File.ReadAllText(args[1]) + "\ncnop\ncnop\n", null, true);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.GetType().Name + ": " + e.Message);
            return 1;
        }
        Console.Error.WriteLine("usage: XnaXbox compile <vs_3_0|ps_3_0> <source.fx> <output.bin>");
        Console.Error.WriteLine("       XnaXbox assemble <source.asm> <output.bin>");
        Console.Error.WriteLine("       XnaXbox listing <source.asm>");
        return 2;
    }

    static CompilerMacro[] Defines()
    {
        var defines = new CompilerMacro[2];
        defines[0].Name = "XBOX";
        defines[1].Name = "XBOX360";
        return defines;
    }

    static int Compile(string profile, string sourceFilename, string outputFilename)
    {
        ShaderProfile shaderProfile;
        switch (profile)
        {
            case "vs_3_0": shaderProfile = ShaderProfile.VS_3_0; break;
            case "ps_3_0": shaderProfile = ShaderProfile.PS_3_0; break;
            default:
                Console.Error.WriteLine("unsupported profile " + profile);
                return 2;
        }
        CompiledShader shader = ShaderCompiler.CompileFromSource(File.ReadAllText(sourceFilename), Defines(), null,
            CompilerOptions.None, "main", shaderProfile, TargetPlatform.Xbox360);
        return Finish(shader, outputFilename, false);
    }

    static int Assemble(string source, string outputFilename, bool listing)
    {
        CompiledShader shader = ShaderCompiler.AssembleFromSource(source, Defines(), null, CompilerOptions.None,
            TargetPlatform.Xbox360);
        return Finish(shader, outputFilename, listing);
    }

    static int Finish(CompiledShader shader, string outputFilename, bool listing)
    {
        string messages = shader.ErrorsAndWarnings ?? "";
        if (!shader.Success)
        {
            Console.Error.WriteLine(messages.Trim());
            return 1;
        }
        if (outputFilename != null)
        {
            File.WriteAllBytes(outputFilename, shader.GetShaderCode());
        }
        if (listing)
        {
            Console.Out.Write(messages);
        }
        else if (messages.Trim().Length != 0)
        {
            Console.Error.WriteLine(messages.Trim());
        }
        return 0;
    }
}
'@

$here = $PSScriptRoot
$bin = Join-Path $here 'bin'
$work = Join-Path ([IO.Path]::GetTempPath()) ("xna-xbox-" + [Guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $bin, $work | Out-Null

function Unpack-Msi([string]$msi, [string]$target) {
    $process = Start-Process msiexec -ArgumentList '/a', "`"$msi`"", '/qn', "TARGETDIR=`"$target`"" -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "msiexec /a $msi failed: $($process.ExitCode)" }
}

try {
    & $SevenZip x -y "-o$work\setup" $Installer | Out-Null
    Unpack-Msi "$work\setup\redists.msi" "$work\redists"
    $redist = Get-ChildItem -Recurse -Path "$work\redists" -Filter xnafx31_redist.msi | Select-Object -First 1
    Unpack-Msi $redist.FullName "$work\xnafx31"

    # The framework, its native half (which holds the Xenos compiler), and the
    # DirectX runtime DLLs the framework imports.
    $runtime = "$work\xnafx31"
    Copy-Item (Get-ChildItem -Recurse -Path $runtime -Filter Microsoft.Xna.Framework.dll | Select-Object -First 1).FullName $bin
    Copy-Item (Get-ChildItem -Recurse -Path $runtime -Filter XnaNative.dll | Select-Object -First 1).FullName $bin
    $dxRedist = (Get-ChildItem -Recurse -Path $runtime -Filter DXSETUP.exe | Select-Object -First 1).DirectoryName
    foreach ($cab in 'OCT2006_d3dx9_31_x86.cab', 'APR2007_xinput_x86.cab', 'Mar2009_X3DAudio_x86.cab') {
        & $SevenZip e -y "-o$work\dx" (Join-Path $dxRedist $cab) | Out-Null
    }
    foreach ($dll in 'd3dx9_31.dll', 'xinput1_3.dll', 'X3DAudio1_6.dll') {
        Copy-Item (Join-Path "$work\dx" $dll) $bin
    }

    # x86 only, and the .NET 2.0 mixed-mode framework loads into .NET 4 only with
    # the legacy activation policy.
    Set-Content -Path "$work\XnaXbox.cs" -Value $source
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    & $csc /nologo /platform:x86 "/r:$bin\Microsoft.Xna.Framework.dll" "/out:$bin\XnaXbox.exe" "$work\XnaXbox.cs"
    if ($LASTEXITCODE -ne 0) { throw 'csc failed' }
    Set-Content -Path "$bin\XnaXbox.exe.config" -Value @'
<?xml version="1.0"?>
<configuration>
  <startup useLegacyV2RuntimeActivationPolicy="true">
    <supportedRuntime version="v4.0"/>
  </startup>
</configuration>
'@
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
Write-Host "Built $bin\XnaXbox.exe. Point XNA_XBOX_EXE at it to run the Xbox 360 tests."
