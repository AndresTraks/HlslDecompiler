float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return float4(ddx_coarse(texcoord.x), ddy_coarse(texcoord.y), ddx_fine(texcoord.x), ddy_fine(texcoord.y)) + rcp(texcoord.x);
}
