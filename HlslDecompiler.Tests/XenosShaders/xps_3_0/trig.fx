float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(sin(texcoord.x), cos(texcoord.y), tan(texcoord.z), atan2(texcoord.w, texcoord.x));
}
