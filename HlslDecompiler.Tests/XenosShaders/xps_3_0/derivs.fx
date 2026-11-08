float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return float4(ddx(texcoord), ddy(texcoord));
}
