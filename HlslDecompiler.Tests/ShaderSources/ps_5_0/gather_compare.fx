SamplerComparisonState shadowSampler;
SamplerState samp;
Texture2D shadowMap;
Texture2D albedo;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	return shadowMap.GatherCmp(shadowSampler, texcoord.xy, texcoord.z) + albedo.Gather(samp, texcoord.xy, int2(1, -1));
}
