float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return float4(ddx(texcoord.x), ddy(texcoord.y), 1, 0);
}
