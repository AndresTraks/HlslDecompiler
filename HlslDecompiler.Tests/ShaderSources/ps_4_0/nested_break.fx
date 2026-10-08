float4 search;

SamplerState samp;
Texture2D field;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		float t2 = (float)t1;
		for (int t3 = 0; t3 < 4; t3 = t3 + 1) {
			float2 t4 = float2((float)t3, t2) * search.zw + texcoord;
			float t5 = field.SampleLevel(samp, t4, 0).x;
			float t6 = max(t0.x, t5);
			if (search.x < t5) {
				t0 = float3(t6, t4);
				break;
			}
			t0 = float3(t6, t4);
		}
		if (search.y < t0.x) {
			break;
		}
	}
	return float4(t0.yzx, 1);
}
