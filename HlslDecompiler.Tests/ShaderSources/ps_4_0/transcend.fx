float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return log(texcoord.y) + exp(texcoord.x) + pow(texcoord, 2.5) + sqrt(texcoord.z) + frac(texcoord.w);
}
