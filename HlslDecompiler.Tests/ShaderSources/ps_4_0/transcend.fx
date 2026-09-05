float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return 0.693147182 * log2(texcoord.y) + exp2(1.44269502 * texcoord.x) + exp2(2.5 * log2(texcoord)) + sqrt(texcoord.z) + frac(texcoord.w);
}
