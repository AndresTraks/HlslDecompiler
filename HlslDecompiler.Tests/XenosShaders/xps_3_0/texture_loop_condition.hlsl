float4 k[8];
int n : register(i0);
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float4 t1 = 0;
	[loop]
	for (int i = 0; i < n; i++) {
		float4 t2 = tex2D(s, texcoord + k[i].xy);
		t1 = t2.wzyx * k[i].z + t0.wzyx;
		t0 = t0 + t2 * k[i].z;
	}
	if (t1.x > 0.5) {
		t1.wzy = sqrt(abs(t1.wzy));
	}
	return t1.wzyx;
}
