float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t0 = 0;
	int t1 = 0;
	for (int t2 = 0; t2 < steps; t2 = t2 + 1) {
		float t3 = tex.SampleLevel(samp, k.xy * (float2)t2 + texcoord, 0).x;
		t0 = k.z < t3 ? t0 + t3 : t0;
		t1 = k.z < t3 ? t1 + 1 : t1;
	}
	return float4(t0 / max((float)t1, 1), (float)t1, saturate(t0 / max((float)t1, 1)), k.w);
}
