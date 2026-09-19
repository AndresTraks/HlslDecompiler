float4 parallax;

SamplerState samp;
Texture2D heightMap;
Texture2D albedoMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float t2 = max(abs(i.texcoord1.z / length(i.texcoord1)), 0.00100000005);
	float2 t3 = i.texcoord1.xy / length(i.texcoord1) * parallax.x / t2 / parallax.y;
	float2 t0 = ddx(i.texcoord);
	float2 t1 = ddy(i.texcoord);
	float t4 = 1 / parallax.y;
	float t5 = heightMap.SampleGrad(samp, i.texcoord, t0, t1).x;
	float2 t6 = i.texcoord;
	float t7 = 1;
	for (int t8 = 0; t8 < 16; t8 = t8 + 1) {
		if (t5 >= t7) {
			break;
		}
		t6 = t6 - t3;
		t5 = heightMap.SampleGrad(samp, t6, t0, t1).x;
		t7 = t7 - t4;
	}
	return albedoMap.Sample(samp, t6) * saturate(t7);
}
