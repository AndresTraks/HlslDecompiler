float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	return float4(ddx(texcoord.x), ddy(texcoord.y), 1, 0);
}
