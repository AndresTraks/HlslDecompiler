float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = ddy_fine(texcoord.y);
	float t1 = ddx_fine(texcoord.x);
	float t2 = ddy_coarse(texcoord.y);
	float t3 = ddx_coarse(texcoord.x);
	return float4(t3, t2, t1, t0) + rcp(texcoord.x);
}
