float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(dot(texcoord.yz, texcoord.zw) + 1, 2, 3, 4);
}
