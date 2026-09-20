float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return 2 * step(0.5, texcoord) + (0.25 >= texcoord ? 8.0 : 0);
}
