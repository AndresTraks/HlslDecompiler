float4 search;

SamplerState samp;
Texture2D field;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		float3 t2 = t0;
		float t3 = (float)t1;
		for (int t4 = 0; t4 < 4; t4 = t4 + 1) {
			float2 t5 = float2((float)t4, t3) * search.zw + texcoord;
			float t6 = field.SampleLevel(samp, t5, 0).x;
			float t7 = max(t2.x, t6);
			if (search.x < t6) {
				t2 = float3(t7, t5);
				break;
			}
			t2 = float3(t7, t5);
		}
		if (search.y < t2.x) {
			t0 = t2;
			break;
		}
		t0 = t2;
	}
	return float4(t0.yzx, 1);
}
