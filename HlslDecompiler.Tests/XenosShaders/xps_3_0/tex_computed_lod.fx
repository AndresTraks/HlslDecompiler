// getCompTexLOD has no HLSL intrinsic: XNA compiles the microcode, and fxc
// the LOD it stands for, with the texture size the translation declares.
// The interpreter takes ddx and ddy as 0, so fxc's compile agrees on the
// colour, not the LOD; the LOD was checked on WARP, with real derivatives.
sampler2D s;
float2 s_size;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float t0 = 0;
#ifdef XBOX
	asm {
		getCompTexLOD2D t0.x, texcoord.yx, s
	};
#else
	t0 = log2(max(length(ddx(texcoord.yx) * s_size), length(ddy(texcoord.yx) * s_size)));
#endif
	return t0 < 1 ? float4(1, 0, 0, 1) : t0 < 2 ? float4(0, 1, 0, 1) : float4(0, 0, 1, 1);
}
