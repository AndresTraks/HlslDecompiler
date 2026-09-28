float4 main(float2 texcoord : TEXCOORD) : SV_TARGET
{
	double t0 = (double)texcoord.x * 2 + 1.5;
	return float4((float)((double)texcoord.x * 0.25 + 1.5) + (float)t0, (float)((double)texcoord.y * 3 + 1.5) + (float)t0, 4 < t0 ? 1.0 : 0, 0);
}
