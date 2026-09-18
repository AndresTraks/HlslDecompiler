float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = 0;
	for (int t1 = 0; t1 < steps; t1 = t1 + 1) {
		float t2 = tex.SampleLevel(samp, k.xy * (float2)t1 + texcoord, 0).x;
		t0 = k.z < t2 ? t0 + float2(t2, 1) : t0;
	}
	return float4(t0.x / max((float)t0.y, 1), (float)t0.y, saturate(t0.x / max((float)t0.y, 1)), k.w);
}
