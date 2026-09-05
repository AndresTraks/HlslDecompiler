float4 m[4];
int n;

float4 main() : POSITION
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.x = 0;
	r0.x = (r0.x < n.x) ? 1 : 0;
	r0.y = (r0.x < n.x) ? 1 : 0;
	r1.x = lerp(2, r0.x, r0.y);
	r0.z = (r1.x < n.x) ? 1 : 0;
	r0.z = r0.z * r0.y;
	r2.x = lerp(3, r1.x, r0.z);
	r0.w = (r2.x < n.x) ? 1 : 0;
	r0.w = r0.w * r0.z;
	r1 = 0;
	for (int i0 = 0; i0 < n; i0++) {
		r2 = r0.x * m + r1;
		r2 = r0.y * m + r2;
		r2 = r0.z * m + r2;
		r1 = r0.w * m + r2;
	}
	o = r1;

	return o;
}
