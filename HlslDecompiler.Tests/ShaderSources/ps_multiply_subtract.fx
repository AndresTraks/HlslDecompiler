float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(3, 2 * texcoord.xy - 1, abs(texcoord.w));
}
