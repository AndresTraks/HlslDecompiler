float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = texcoord.x / texcoord.y;
	return float4(t0 != t0 ? 1.0 : 0, (asint(t0) & 2147483647) == 2139095040 ? 1.0 : 0, (asint(t0) & 2139095040) != 2139095040 ? 1.0 : 0, (float)sign(t0));
}
