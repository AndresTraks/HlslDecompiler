float4 colour;
int n;

float4 main(float color : COLOR) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = 0;
	for (int i0 = 0; i0 < n; i0++) {
		if (0.5 < color.x) {
		} else {
			r1 = r0;
			for (int i1 = 0; i1 < n; i1++) {
				r1 = r1 + colour;
			}
			r0 = r1;
		}
	}
	o = r0;

	return o;
}
