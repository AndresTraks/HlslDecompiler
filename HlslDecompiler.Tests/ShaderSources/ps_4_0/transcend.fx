float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return 0.693147182 * log2(texcoord.y) + exp2(1.44269502 * texcoord.x) + pow(texcoord, 2.5) + sqrt(texcoord.z) + frac(texcoord.w);
}
