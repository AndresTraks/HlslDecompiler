SamplerComparisonState shadowSampler;
SamplerState samp;
Texture2D shadowMap;
Texture2D albedo;

float4 main(float4 texcoord : TEXCOORD) : SV_TARGET
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = shadowMap.GatherCmp(shadowSampler, texcoord.xy, texcoord.z);
	r1 = albedo.Gather(samp, texcoord.xy, int2(1, -1));
	o = r0 + r1;

	return o;
}
