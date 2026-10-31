bool b : register(b0);
float4 k[4];
float4 m;
int n : register(i0);

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	if (b) {
		r0 = position;
		[loop]
		for (int i0 = 0; i0 < n; i0++) {
			r0 = r0 + k[i0];
		}
	} else {
		r0 = position;
	}
	r1 = r0 * 0.15915494 + 0.25;
	r1 = frac(r1);
	r1 = r1 * 6.2831855 + -3.1415927;
	r1 = r1 * r1;
	r2 = r1 * -2.523985E-07 + 2.47609E-05;
	r2 = r1 * r2 + -0.0013888397;
	r2 = r1 * r2 + 0.04166664;
	r2 = r1 * r2 + -0.5;
	r1 = r1 * r2 + 1;
	o = r1 * m + r0;

	return o;
}
