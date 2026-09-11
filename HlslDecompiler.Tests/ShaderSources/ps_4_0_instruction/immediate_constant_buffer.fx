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
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = texcoord.xy;
	r1 = float4(0, 0, 0, 0);
	r0.w = 0;
	while (true) {
		r2.x = (r0.w >= 4) ? -1 : 0;
		if (r2.x != 0) break;
		r0.z = r0.w;
		r2 = layers.Sample(samp, r0.xyz);
		r0.z = dot(weights, icb[r0.w]);
		r1 = r2 * r0.z + r1;
		r0.w = r0.w + 1;
	}
	o = r1;

	return o;
}
