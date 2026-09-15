float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	r0.x = -k.x + k.y;
	r0.x = 1 / r0.x;
	r0.y = 1 / k.w;
	r1 = 0;
	r0.z = 0;
	for (int i0 = 0; i0 < 3; i0++) {
		r2 = texcoord * r0.z + -k.x;
		r2 = saturate(r0.x * r2);
		r3 = r2 * -2 + 3;
		r2 = r2 * r2;
		r2 = r2 * r3;
		r3 = -r0.z + texcoord;
		r4 = (-r3 >= 0) ? 0 : 1;
		r3 = (r3 >= 0) ? -0 : -1;
		r3 = r3 + r4;
		r2 = r2 * r3 + r1;
		r0.w = r0.y * r2.x;
		r3.x = frac(abs(r0.w));
		r0.w = (r0.w >= 0) ? r3.x : -r3.x;
		r0.w = r0.w * -k.w + k.z;
		r3 = max(r2, -k);
		r4 = min(k, r3);
		r1 = (r0.w >= 0) ? r2 : r4;
		r0.z = r0.z + 1;
	}
	o = r1;

	return o;
}
