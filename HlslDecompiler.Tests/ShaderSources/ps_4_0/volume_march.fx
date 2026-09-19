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
		float t5 = volume.SampleLevel(samp, t1, 0).x * march.y;
		float t6 = t5 * (1 - t3);
		float3 t7 = t6 * march.z + t2;
		float t8 = lerp(t3, 1, t5);
		if (march.w < t8) {
			t2 = t7;
			t3 = t8;
			break;
		}
		t1 = t0 * march.x + t1;
		t2 = t7;
		t3 = t8;
	}
	return float4(t2, t3);
}
