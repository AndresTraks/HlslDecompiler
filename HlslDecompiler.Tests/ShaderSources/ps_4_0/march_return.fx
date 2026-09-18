float4 limits;

SamplerState samp;
Texture2D heights;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = float3(texcoord, 0);
	for (int t1 = 0; t1 < 16; t1 = t1 + 1) {
		t0.z = heights.SampleLevel(samp, t0.xy, 0).x * limits.x + t0.z;
		if (limits.y < t0.z) {
			return float4(t0.z, (float)t1, 1, 1);
		}
		t0.xy = t0.xy + limits.zw;
	}
	return float4(t0.z, 16, 0, 1);
}
