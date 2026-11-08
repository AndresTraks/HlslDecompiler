float4 k;
int m : register(i1);
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < n; i++) {
		for (int j = 0; j < m; j++) {
			t0 = t0 + texcoord * k * (i + j);
		}
	}
	return t0;
}
