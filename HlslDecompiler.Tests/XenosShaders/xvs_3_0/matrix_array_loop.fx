float4x4 m[4];
int n;

float4 main(float4 position : POSITION) : POSITION
{
	float4 t0 = position;
	for (int i = 0; i < n; i++) {
		t0 = mul(t0, m[i]);
	}
	return t0;
}
