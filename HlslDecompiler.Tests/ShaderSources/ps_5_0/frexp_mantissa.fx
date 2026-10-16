float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = texcoord.xz * float2(1, 0.333333343);
	return float4(cos(t0.x) + sin(t0.x), texcoord.y, 3 * (t0.y >= -t0.y ? frac(abs(t0.y)) : -frac(abs(t0.y))) + exp2(texcoord.y) * t0.x, (float)((texcoord.w != 0 ? (asint(texcoord.w) & 2139095040) - 1056964608 : 0) >> 23) + asfloat(texcoord.w != 0 ? (1056964608 & ~8388607) | (asint(texcoord.w) & 8388607) : 0));
}
