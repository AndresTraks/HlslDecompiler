float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = texcoord.x / texcoord.y;
	return float4(isnan(t0) ? 1.0 : 0, isinf(t0) ? 1.0 : 0, isfinite(t0) ? 1.0 : 0, (float)sign(t0));
}
