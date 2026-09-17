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
	float2 t2 = i.texcoord1.xy / length(i.texcoord1) * parallax.x / max(abs(i.texcoord1.z / length(i.texcoord1)), 0.00100000005) / parallax.y;
	float2 t0 = ddx(i.texcoord);
	float2 t1 = ddy(i.texcoord);
	float t3 = 1 / parallax.y;
	float t4 = heightMap.SampleGrad(samp, i.texcoord, t0, t1).x;
	float2 t5 = i.texcoord;
	float t6 = 1;
	for (int t7 = 0; t7 < 16; t7 = t7 + 1) {
		if (t4 >= t6) {
			break;
		}
		t5 = t5 - t2;
		t4 = heightMap.SampleGrad(samp, t5, t0, t1).x;
		t6 = t6 - t3;
	}
	return albedoMap.Sample(samp, t5) * saturate(t6);
}
