float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return saturate(1 / texcoord) + (0.5 >= texcoord ? 1 : 0);
}
