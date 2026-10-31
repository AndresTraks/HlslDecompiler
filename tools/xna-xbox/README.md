# XnaXbox

A small command line front end to the Xbox 360 shader compiler in XNA Game
Studio 3.1, the only Xenos compiler available outside the Xbox 360 XDK. The
Xbox 360 tests use it to assemble the decompiler's listings back into shaders,
and it compiles the HLSL behind the fixtures in
`HlslDecompiler.Tests/XenosShaders`.

```
XnaXbox compile <vs_3_0|ps_3_0> <source.fx> <output.bin>
XnaXbox assemble <source.asm> <output.bin>
XnaXbox listing <source.asm>
```

Only the build script is in the repository. XNA's files cannot be: its license
allows one copy per device and no redistribution. The script takes them from
Microsoft's installer, which you download yourself.

## Building

Requirements: Windows, the .NET Framework 4 (part of Windows), and
[7-Zip](https://www.7-zip.org/) in `C:\Program Files\7-Zip` (or pass
`-SevenZip <path to 7z.exe>`).

1. Download `XNAGS31_setup.exe` (73.2 MB) from Microsoft's
   [XNA Game Studio 3.1 page](https://www.microsoft.com/download/details.aspx?id=39).
   It is signed by Microsoft Corporation. The copy these instructions were
   written with had the SHA-256
   `3321f6d3e88c063a9e354293867781b9fd71f5049180f9271bf312d8343d225e`.
2. From the repository root, run:
   ```
   powershell -ExecutionPolicy Bypass -File tools\xna-xbox\build.ps1 -Installer C:\path\to\XNAGS31_setup.exe
   ```

Nothing is installed, and Visual Studio 2008 is not needed. The installer is
unpacked into a temporary folder with 7-Zip and an administrative `msiexec /a`.
The script copies what the compiler needs into `tools\xna-xbox\bin`, which git
ignores:

- `Microsoft.Xna.Framework.dll` and `XnaNative.dll`, from the XNA Framework 3.1
  redistributable inside the installer. `XnaNative.dll` holds the Xenos compiler.
- `d3dx9_31.dll`, `xinput1_3.dll` and `X3DAudio1_6.dll`, from the DirectX cabinets
  bundled with it, which the framework imports.

It then compiles `XnaXbox.exe` (32-bit, since XNA is) with the .NET Framework's
`csc.exe`. Its source is inside `build.ps1` rather than in a `.cs` file, because
the main project compiles every `.cs` file under the repository.

## Using it

The tests look for `tools\xna-xbox\bin\XnaXbox.exe`, or for the path in
`XNA_XBOX_EXE`. Without it, the round-trip tests are skipped and the golden
listings are still checked.
