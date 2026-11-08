float3 c0;
float3 c1;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	[loop]
	for (int i = 0; i < 2; i++) {
		float3 t1;
		[branch]
		if (t0.y != 0) {
			t1 = c1;
		} else {
			t1 = c0;
		}
		t0 = t0 + float4(t1.x * texcoord.x, 1, t1.yz * texcoord.yz);
	}
	return float4(t0.xzw, 1);
}
