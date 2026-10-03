float4 weights;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	int t1 = 0;
	while (t1 < 8) {
		float3 t2 = source.SampleLevel(samp, weights.zw * (float2)t1 + texcoord, 0).xyz;
		float3 t3 = t2 * weights.x + t0.xyz;
		if (source.SampleLevel(samp, weights.zw * (float2)t1 + texcoord, 0).x < weights.y) {
			t0.xyz = t3;
			t1 = t1 + 1;
			continue;
		}
		t0 = float4(t3, t0.w + 1);
		t1 = t1 + 1;
	}
	return float4(t0.xyz / max(t0.w, 1), t0.w);
}
