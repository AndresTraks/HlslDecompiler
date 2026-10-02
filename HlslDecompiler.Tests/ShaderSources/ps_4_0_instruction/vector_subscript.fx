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
	float4 o;

	int3 r0;
	float4 r1;
	r0.x = i.band & 3;
	r0.x = asint(dot(bandWeights, icb[r0.x]));
	r0.y = (uint)channel % 3;
	r0.z = asint(dot(channelScale.xyz, icb[r0.y].xyz));
	r0.x = asint(asfloat(r0.z) * asfloat(r0.x));
	r1 = gbuffer.Sample(pointSampler, i.texcoord.xy);
	r0.y = asint(dot(r1.xyz, icb[r0.y].xyz));
	o.w = r1.w;
	o.xyz = asfloat(r0.xxx) * asfloat(r0.yyy);

	return o;
}
