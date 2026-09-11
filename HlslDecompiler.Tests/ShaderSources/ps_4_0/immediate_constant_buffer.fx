float4 weights;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerState samp;
Texture2DArray layers;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		t0 = layers.Sample(samp, float3(texcoord, (float)t1)) * dot(weights, icb[t1]) + t0;
	}
	return t0;
}
