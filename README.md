# HlslDecompiler
Decompiles Direct3D shader bytecode into HLSL code

## Supported shader models
Vertex shaders from vs_1_1 to vs_5_0 and pixel shaders from ps_2_0 to ps_5_0,
along with the later stages: geometry (gs_4_0 to gs_5_0), compute (cs_4_0 to
cs_5_0), hull (hs_5_0) and domain (ds_5_0) shaders. Each has shaders in the
test corpus, which recompiles every decompilation and compares what the two
of them compute.

Effects (fx_2_0, fx_4_0, fx_4_1 and fx_5_0) are decompiled whole, into one
effect.fx: the parameters and buffers with their defaults, semantics and
annotations, the textures, samplers and state objects, a function for each
shader, and the techniques, passes and groups with the states they set.
Compiled again, it is the same effect. The disassembly is written a shader at a
time, effect_vs0.asm, effect_ps0.asm and so on.

In fx_2_0, arithmetic on uniforms alone is moved out of the shader into a
preshader that runs before it; the decompilation puts it back in the shader.

## Usage
`HlslDecompiler [--ast] [--print] shader.fxc`

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
It does this by taking the shader bytecode, constructing an abstract syntax tree, simplifying it and compiling to HLSL:
```hlsl
float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return float4(-abs(texcoord.z), texcoord.x, 1, 2);
}
```
