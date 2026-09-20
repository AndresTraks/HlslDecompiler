float4 colour;
int n;

float4 main() : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float r2;
	float4 r3;
	r0 = 0;
	for (int i0 = 0; i0 < n; i0++) {
		if (5 < r0.w) {
		} else {
			r1 = r0.wxyz;
			for (int i1 = 0; i1 < n; i1++) {
				r2 = -r1.y + 3;
				r3 = r1 + colour;
				r1 = (r2.x >= 0) ? r3 : r1;
			}
			r0 = r1.yzwx;
		}
	}
	o.yzw = r0.xyz;
	o.x = r0.w;

	return o;
}
