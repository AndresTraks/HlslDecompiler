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
	float3 t2 = normalize(i.texcoord1);
	float t3 = max(abs(t2.z), 0.00100000005);
	float2 t4 = t2.xy * parallax.x / t3 / parallax.y;
	float2 t0 = ddx(i.texcoord);
	float2 t1 = ddy(i.texcoord);
	float t5 = 1 / parallax.y;
	float t6 = heightMap.SampleGrad(samp, i.texcoord, t0, t1).x;
	float2 t7 = i.texcoord;
	float t8 = 1;
	for (int t9 = 0; t9 < 16; t9 = t9 + 1) {
		if (t6 >= t8) {
			break;
		}
		t7 = t7 - t4;
		t6 = heightMap.SampleGrad(samp, t7, t0, t1).x;
		t8 = t8 - t5;
	}
	return albedoMap.Sample(samp, t7) * saturate(t8);
}
