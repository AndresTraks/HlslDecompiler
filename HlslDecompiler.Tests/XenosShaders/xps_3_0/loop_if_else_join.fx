float3 c0;
float3 c1;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float3 t0 = 0;
	[loop]
	for (int i = 0; i < 2; i++) {
		float3 t1;
		[branch]
		if (i != 0) {
			t1 = c1;
		} else {
			t1 = c0;
		}
		t0 = t0 + t1 * texcoord;
	}
	return float4(t0, 1);
}
