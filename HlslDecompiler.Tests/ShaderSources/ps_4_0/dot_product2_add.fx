float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(dot(texcoord.yz, texcoord.zw) + 1, 2, 3, 4);
}
