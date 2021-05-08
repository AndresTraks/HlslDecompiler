$generateAssemblyListing = $False;

$fxc_paths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\8.1\bin\x64\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x86\fxc.exe",
    "${env:ProgramFiles(x86)}\Windows Kits\8.1\bin\x86\fxc.exe"
);

function FindFxc {
    $fxc_paths | Where { Test-Path -Path $_ -PathType Leaf } | Select -First 1
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

function CompileShader($basename, $profile, $fxc) {
    Write-Host "Compiling $basename..."
    if ($generateAssemblyListing) {
        $assemblyListingArg = " /Fc ShaderAssembly\$basename.asm"
    } else {
        $assemblyListingArg = ""
    }
    $arguments = "/T $profile ShaderSources/$basename.fx /Fo CompiledShaders/$basename.fxc$assemblyListingArg"
    RunProgram $fxc $arguments
}

function CompileByType($shaderType, $fxc) {
    $profile = "$($shaderType)_3_0"
    ForEach ($shaderSource in Get-ChildItem "ShaderSources\$($shaderType)_*.fx") {
        CompileShader $shaderSource.Basename $profile $fxc
    }
}

function CompileAll {
    $fxc = FindFxc
    if (-Not $fxc) {
        Write-Error "HLSL compiler fxc.exe not found."
        return
    }
    Write-Host "Using $fxc"

    CompileByType "ps" $fxc
    CompileByType "vs" $fxc
}

CompileAll
