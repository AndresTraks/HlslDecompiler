float4 k;
int m : register(i1);
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float4 t1 = 0;
	float t2 = abs(texcoord.x) >= 0 ? 0 : 1;
	[loop]
	for (int i = 0; i < n; i++) {
		float t3 = abs(texcoord.x) >= 0 ? 0 : 1;
		[loop]
		for (int j = 0; j < m; j++) {
			t0 = texcoord.wzyx * k.wzyx;
			t1 = t1 + t0.wzyx * (t2 + t3);
			t3 = t3 + 1;
		}
		t0 = t1.wzyx;
		t2 = t2 + 1;
	}
	return t0.wzyx;
}
