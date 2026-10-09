cbuffer cb : register(b0)
{
	float4 weights;
};

SamplerState samp;
Texture2DArray layers;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		float4 t2 = layers.Sample(samp, float3(texcoord, (float)t1));
		t0 = t0 + t2 * weights[t1];
	}
	return t0;
}
