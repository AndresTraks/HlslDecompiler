float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return 0.25 * (texcoord.wzyx - texcoord) + texcoord + min(max(texcoord, 0.100000001), 0.899999976) + min(texcoord, 0.5) + max(texcoord, 0.200000003);
}
