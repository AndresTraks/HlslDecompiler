float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return float4(texcoord.x, texcoord.y, 0, 1);
}
