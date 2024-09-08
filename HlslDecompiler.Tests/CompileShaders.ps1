$generateAssemblyListing = $False;

$fxc_paths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\fxc.exe"
    "${env:ProgramFiles(x86)}\Windows Kits\8.1\bin\x64\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x86\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x86\fxc.exe"
    "${env:ProgramFiles(x86)}\Windows Kits\8.1\bin\x86\fxc.exe"
);

function FindFxc {
    $fxc_paths | Where { Test-Path -Path $_ -PathType Leaf } | Resolve-Path | Select -First 1
}

function RunProgram($program, $arguments) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $program
    $info.RedirectStandardError = $true
    $info.RedirectStandardOutput = $true
    $info.UseShellExecute = $false
    $info.Arguments = $arguments
    $info.WorkingDirectory = Get-Location
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    $process.Start() | Out-Null
    $process.WaitForExit()
    
    if ($process.ExitCode -ne 0) {
        Write-Error $process.StandardError.ReadToEnd()
    }
}

function CleanAssemblyListing($assemblyFileName) {
    (Get-Content "$assemblyFileName") |
        Where { $_ -ne "" -and $_ -NotMatch "^//*" } |
        Foreach { $_ -Replace  "    ", "" } |
        Foreach { $_ -Replace  "ret ", "ret" } |
        Set-Content "$assemblyFileName"
}

function CompileShader($basename, $profile, $fxc) {
    Write-Host "Compiling $profile\$basename..."
    if ($generateAssemblyListing) {
        $assemblyListingArg = " /Fc ShaderAssembly\$profile\$basename.asm"
    } else {
        $assemblyListingArg = ""
    }
    $arguments = "/T $profile ShaderSources\$profile\$basename.fx /Fo CompiledShaders/$profile/$basename.fxc$assemblyListingArg"
    RunProgram $fxc $arguments

    if ($generateAssemblyListing) {
        CleanAssemblyListing "ShaderAssembly\$profile\$basename.asm"
    }
}

function CompileByProfile($profile, $fxc) {
    ForEach ($shaderSource in Get-ChildItem "ShaderSources\$($profile)\*.fx") {
        CompileShader "$($shaderSource.Basename)" $profile $fxc
    }
}

function CompileAll {
    $fxc = FindFxc
    if (-Not $fxc) {
        Write-Error "HLSL compiler fxc.exe not found."
        return
    }
    Write-Host "Using $fxc"

    CompileByProfile "ps_3_0" $fxc
    CompileByProfile "ps_4_0" $fxc
    CompileByProfile "vs_3_0" $fxc
    CompileByProfile "vs_4_0" $fxc
}

CompileAll
