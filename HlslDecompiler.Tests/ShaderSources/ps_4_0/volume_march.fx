float4 march;

SamplerState samp;
Texture3D volume;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(i.texcoord1);
	float3 t1 = i.texcoord;
	float3 t2 = 0;
	float t3 = 0;
	for (int t4 = 0; t4 < 32; t4 = t4 + 1) {
		float t5 = volume.SampleLevel(samp, t1, 0).x;
		float t6 = t5 * march.y;
		float t7 = t6 * (1 - t3);
		float3 t8 = t7 * march.z + t2;
		float t9 = lerp(t3, 1, t6);
		if (march.w < t9) {
			t2 = t8;
			t3 = t9;
			break;
		}
		t1 = t0 * march.x + t1;
		t2 = t8;
		t3 = t9;
	}
	return float4(t2, t3);
}
