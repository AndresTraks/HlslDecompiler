float4 k[8];
int n : register(i0);
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = (0 < -abs(texcoord.x)) ? 1 : 0;
	float4 t1 = t0;
	float4 t2 = t0;
	[loop]
	for (int i = 0; i < n; i++) {
		float4 t3 = tex2D(s, texcoord + k[i].xy);
		t2 = t3.wzyx * k[i].z + t1.wzyx;
		t1 = t1 + t3 * k[i].z;
	}
	if (((0.5 < t2.x) ? 1 : 0) != 0) {
		t2.wzy = sqrt(abs(t2.wzy));
	}
	return t2.wzyx;
}
