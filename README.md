# HlslDecompiler
Decompiles Direct3D shader bytecode into HLSL code

## Supported shader models
All DXBC and earlier: \
vs_1_1 to vs_5_0 \
ps_2_0 to ps_5_0 \
gs_4_0 to gs_5_0, cs_4_0 to cs_5_0, hs_5_0, ds_5_0 \
fx_2_0, fx_4_0, fx_4_1 and fx_5_0.

Shader Model 6+ (DXIL) is not supported.

## Usage
`HlslDecompiler [options] shader.fxc`

The HLSL is written to stdout or to a file with `-o shader.fx`.
```hlsl
float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-abs(texcoord.z), texcoord.x, 1, 2);
}
```
With `--instructions` it writes one statement per instruction and does not try
to recover high level structures, which stays closer to the bytecode:
```hlsl
float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.x = -abs(texcoord.z);
	o.yzw = texcoord.xxx * float3(1, 0, 0) + float3(0, 1, 2);

	return o;
}
```
With `--asm` it writes the disassembly instead:
```
ps_3_0
def c0, 1, 0, 2, 0
dcl_texcoord v0.xz
mov oC0.x, -v0.z_abs
mad oC0.yzw, v0.xxx, c0.xyy, c0.yxz
```

| Option | |
| --- | --- |
| `-o`, `--output <file>` | Write to `<file>` instead of standard output
| `--asm` | Write the disassembly instead of HLSL |
| `--instructions` | Write HLSL one statement per instruction |
| `--verbose` | Print the stack trace when decompiling fails |
| `-h`, `--help` | Show the options |
