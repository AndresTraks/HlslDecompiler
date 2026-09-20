SamplerComparisonState shadowSampler;
Texture2D shadowMap;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	return shadowMap.SampleCmp(shadowSampler, texcoord.xy, texcoord.z).x;
}
