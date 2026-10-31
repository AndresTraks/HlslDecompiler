bool b : register(b0);
float4 k[4];
float4 m;
int n : register(i0);

float4 main(float4 position : POSITION) : POSITION
{
	float4 t0;
	if (b) {
		t0 = position;
		[loop]
		for (int i = 0; i < n; i++) {
			t0 = t0 + k[i];
		}
	} else {
		t0 = position;
	}
	float4 t1 = 6.28318548 * frac(0.159154937 * t0 + 0.25) - 3.14159274;
	float4 t2 = t1 * t1;
	return (t2 * (t2 * (t2 * (t2 * (-0.000000252398507 * t2 + 0.0000247609005) - 0.00138883968) + 0.0416666418) - 0.5) + 1) * m + t0;
}
