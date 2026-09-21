cbuffer Blur : register(b0)
{
	float2 texelSize;
	int radius;
	float sigma;
	float4 weights[4];
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerState linearSampler;
Texture2D source;
Texture2D depth;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = depth.Sample(linearSampler, texcoord).x;
	if (t0 >= 1) {
		discard;
	}
	float4 t1 = 0;
	float t2 = dot(sigma.xx, sigma.xx);
	int t3 = -radius;
	float t4 = 0;
	[loop]
	while (radius >= t3) {
		float2 t5 = float2((float)t3 * texelSize.x + texcoord.x, texcoord.y);
		if (abs(depth.Sample(linearSampler, t5).x - t0) > 0.00999999978) {
			t3 = t3 + 1;
			continue;
		}
		float t6 = dot(weights[(uint)abs(t3) >> 2], icb[abs(t3) & 3]);
		float t7 = exp((float)(t3 * -t3) / t2);
		float t8 = t7 * t6;
		t1 = source.Sample(linearSampler, t5) * t8 + t1;
		t4 = t6 * t7 + t4;
		t3 = t3 + 1;
	}
	return t4 > 0 ? t1 / t4 : source.Sample(linearSampler, texcoord);
}
