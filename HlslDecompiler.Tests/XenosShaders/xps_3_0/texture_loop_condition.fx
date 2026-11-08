float4 k[8];
int n;
sampler2D s;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		t0 = t0 + tex2D(s, texcoord + k[i].xy) * k[i].z;
	}
	if (t0.w > 0.5) {
		t0.xyz = sqrt(t0.xyz);
	}
	return t0;
}
