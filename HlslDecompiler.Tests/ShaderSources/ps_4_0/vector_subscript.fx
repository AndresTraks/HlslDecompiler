cbuffer Debug : register(b0)
{
	float3 channelScale;
	float4 bandWeights;
	uint channel;
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerState pointSampler;
Texture2D gbuffer;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	nointerpolation uint band : BAND;
};

float4 main(PS_IN i) : SV_Target
{
	float t0 = channelScale[channel % 3] * bandWeights[i.band & 3];
	float t1 = dot(gbuffer.Sample(pointSampler, i.texcoord).xyz, icb[channel % 3].xyz);
	return float4(t0 * t1, t0 * t1, t0 * t1, gbuffer.Sample(pointSampler, i.texcoord).w);
}
