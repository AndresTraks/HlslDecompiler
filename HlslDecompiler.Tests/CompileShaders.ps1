$shaderSources = @(
    "ps_constant",
    "ps_texcoord",
    "ps_texcoord_modifier",
    "ps_texcoord_swizzle",
    "ps_float4_construct",
    "ps_float4_constant",
    "ps_multiply_subtract",
    "ps_absolute_multiply",
    "ps_negate_absolute",
    "ps_tex2d",
    "ps_tex2d_swizzle"
);

$fxc_paths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\x64\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\8.1\bin\x64\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\x86\fxc.exe",
    "C:\Program Files (x86)\Windows Kits\8.1\bin\x86\fxc.exe"
);

function Compile {
    $fxc = $fxc_paths | Where { Test-Path -Path $_ -PathType Leaf } | Select -First 1
    if (-Not $fxc) {
        Write-Error "HLSL compiler fxc.exe not found."
        return
    }

    ForEach ($shaderSource in $shaderSources) {
        & $fxc /T ps_3_0 "ShaderSources/$shaderSource.fx" /Fo "CompiledShaders/$shaderSource.fxc"
    }
}

Compile
