SamplerState samp;
SamplerComparisonState shadowSampler;
Texture2D albedo;
Texture2D shadowMap;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int2 offset : OFFSET;
};

float4 main(PS_IN i) : SV_TARGET
{
	return albedo.Gather(samp, i.texcoord.xy, i.offset) + shadowMap.GatherCmp(shadowSampler, i.texcoord.xy, i.texcoord.z, i.offset);
}
