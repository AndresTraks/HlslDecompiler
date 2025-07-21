# HlslDecompiler
Decompiles Shader Model 3.0 shaders into HLSL code

## Usage
`HlslDecompiler [--ast] shader.fxc`
`HlslDecompiler [--print] shader.fxc`

The program will output the assembly listing in shader.asm, e.g.
```
ps_3_0
def c0, 1, 0, 2, 0
dcl_texcoord v0.xz
mov oC0.x, -v0.z_abs
mad oC0.yzw, v0.xxx, c0.xyy, c0.yxz
```
and the decompiled HLSL code in shader.fx:
```hlsl
float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o.x = -abs(texcoord.z);
	o.yzw = texcoord.xxx * float3(1, 0, 0) + float3(0, 1, 2);

	return o;
}
```

With the --ast option, the program will attempt generate more readable HLSL.
It does this by taking the shader bytecode, constructing an abstract syntax tree, simplyfying it and compiling to HLSL:
```hlsl
float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-abs(texcoord.z), texcoord.x, 1, 2);
}
```