$generateAssemblyListing = $False;

$pixelShaderSources = @(
    "ps_constant",
    "ps_constant_struct",
    "ps_texcoord",
    "ps_texcoord_modifier",
    "ps_texcoord_swizzle",
    "ps_float4_construct",
    "ps_float4_constant",
    "ps_multiply_subtract",
    "ps_absolute_multiply",
    "ps_negate_absolute",
    "ps_tex2d",
    "ps_tex2d_swizzle",
    "ps_tex2d_two_samplers"
);

$vertexShaderSources = @(
    "vs_constant",
    "vs_constant_struct",
    "vs_dot_product",
    "vs_length",
    "vs_matrix22_vector2_multiply",
    "vs_matrix33_vector3_multiply",
    "vs_matrix44_vector4_multiply",
    "vs_normalize",
    "vs_vector2_matrix22_multiply",
    "vs_vector3_matrix33_multiply",
    "vs_vector4_matrix44_multiply"
);

$fxc_paths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\x64\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\8.1\bin\x64\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\x86\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\8.1\bin\x86\fxc.exe"
);

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

function Compile {
    $fxc = $fxc_paths | Where { Test-Path -Path $_ -PathType Leaf } | Select -First 1
    if (-Not $fxc) {
        Write-Error "HLSL compiler fxc.exe not found."
        return
    }

    ForEach ($shaderSource in $pixelShaderSources) {
        Write-Host "Compiling $shaderSource..."
        if ($generateAssemblyListing) {
            $assemblyListingArg = " /Fc ShaderAssembly/$shaderSource.asm"
        } else {
            $assemblyListingArg = ""
        }
        $arguments = "/T ps_3_0 ShaderSources/$shaderSource.fx /Fo CompiledShaders/$shaderSource.fxc$assemblyListingArg"
        RunProgram $fxc $arguments
    }
    
    ForEach ($shaderSource in $vertexShaderSources) {
        Write-Host "Compiling $shaderSource..."
        if ($generateAssemblyListing) {
            $assemblyListingArg = " /Fc ShaderAssembly/$shaderSource.asm"
        } else {
            $assemblyListingArg = ""
        }
        $arguments = "/T vs_3_0 ShaderSources/$shaderSource.fx /Fo CompiledShaders/$shaderSource.fxc$assemblyListingArg"
        RunProgram $fxc $arguments
    }
}

Compile
