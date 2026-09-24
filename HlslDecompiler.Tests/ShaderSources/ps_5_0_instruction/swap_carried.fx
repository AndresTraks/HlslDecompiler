float4 v;
int n;

float4 main() : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int2 r2;
	float4 r3;
	r0 = v;
	r1 = v.wzyx;
	r2.x = 0;
	while (true) {
		r2.y = (r2.x >= n) ? -1 : 0;
		if (r2.y != 0) break;
		r3 = r0 + float4(0.25, 0.25, 0.25, 0.25);
		r0 = r1 * float4(0.5, 0.5, 0.5, 0.5);
		r2.x = r2.x + 1;
		r1 = r3;
	}
	o = r1 * float4(2, 2, 2, 2) + r0;

	return o;
}
