float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return float4(floor(texcoord.x), ceil(texcoord.y), round(texcoord.z), trunc(texcoord.w));
}
