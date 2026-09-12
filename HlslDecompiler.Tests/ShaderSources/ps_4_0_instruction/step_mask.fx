float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int4 r2;
	int4 r3;
	r0.x = -(k.x) + k.y;
	r0.x = float1(1) / r0.x;
	r1 = texcoord + -(k.x);
	r0 = saturate(r0.x * r1);
	r1 = r0 * float4(-2, -2, -2, -2) + float4(3, 3, 3, 3);
	r0 = r0 * r0;
	r2 = max(texcoord, k.z);
	r2 = min(r2, k.w);
	r0 = r1 * r0 + r2;
	r1 = texcoord + -(k);
	r2 = (float4(0, 0, 0, 0) < r1) ? -1 : 0;
	r3 = (r1 < float4(0, 0, 0, 0)) ? -1 : 0;
	r1 = k.w * r1 + k;
	r2 = -(r2) + r3;
	r2 = (float4)(int4)r2;
	r0 = r0 + r2;
	r2 = texcoord / -(k.x);
	r3 = (r2 >= -(r2)) ? -1 : 0;
	r2 = frac(abs(r2));
	r2 = (r3 != 0) ? r2 : -(r2);
	r0 = r2 * -(k.x) + r0;
	r2 = (texcoord >= k.y) ? -1 : 0;
	r2 = r2 & int4(1065353216, 1065353216, 1065353216, 1065353216);
	r0 = r0 + r2;
	o = r1 + r0;

	return o;
}
