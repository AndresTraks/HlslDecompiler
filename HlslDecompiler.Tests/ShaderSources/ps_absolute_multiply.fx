float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(3 * texcoord.xw - 1, 8, abs(3 * texcoord.x));
}
