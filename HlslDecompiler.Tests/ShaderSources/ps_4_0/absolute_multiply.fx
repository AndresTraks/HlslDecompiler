float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(3 * texcoord.xw - 1, 8, abs(3 * texcoord.x));
}
