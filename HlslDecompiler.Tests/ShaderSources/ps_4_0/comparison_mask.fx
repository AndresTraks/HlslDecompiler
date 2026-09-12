float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return 2 * (texcoord >= 0.5 ? 1 : 0) + (0.25 >= texcoord ? 8 : 0);
}
