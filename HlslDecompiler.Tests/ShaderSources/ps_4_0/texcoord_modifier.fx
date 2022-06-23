float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(-texcoord.yx, 2, abs(texcoord.w));
}
