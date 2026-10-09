SamplerComparisonState shadowSampler;
SamplerState samp;
Texture2D shadowMap;
Texture2D albedo;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	float4 t0 = albedo.Gather(samp, texcoord.xy, int2(1, -1));
	float4 t1 = shadowMap.GatherCmp(shadowSampler, texcoord.xy, texcoord.z);
	return t0 + t1;
}
