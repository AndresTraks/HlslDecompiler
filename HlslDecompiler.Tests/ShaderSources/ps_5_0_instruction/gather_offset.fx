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
	float4 o;

	float4 r0;
	float4 r1;
	r0 = albedo.Gather(samp, i.texcoord.xy, i.offset.xy);
	r1 = shadowMap.GatherCmp(shadowSampler, i.texcoord.xy, i.texcoord.z, i.offset.xy);
	o = r0 + r1;

	return o;
}
