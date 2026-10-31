float4 k;
int m : register(i1);
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = (0 < -abs(texcoord.x)) ? 1 : 0;
	float4 t1 = t0;
	float4 t2 = t0;
	float t3 = abs(texcoord.x) >= 0 ? 0 : 1;
	for (int i = 0; i < n; i++) {
		float t4 = abs(texcoord.x) >= 0 ? 0 : 1;
		for (int j = 0; j < m; j++) {
			t2 = texcoord.wzyx * k.wzyx;
			t1 = t1 + t2.wzyx * (t3 + t4);
			t4 = t4 + 1;
		}
		t2 = t1.wzyx;
		t3 = t3 + 1;
	}
	return t2.wzyx;
}
