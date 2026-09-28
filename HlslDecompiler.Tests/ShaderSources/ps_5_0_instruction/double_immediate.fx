float4 main(float2 texcoord : TEXCOORD) : SV_TARGET
{
	float4 o;

	int4 r0;
	double2 d0;
	float r1;
	d0 = (double2)texcoord.xy;
	d0 = d0 * double2(0.25, 3);
	d0 = d0 + 1.5;
	r0.xy = asint((float2)d0);
	d0.y = (double)texcoord.x;
	d0.y = d0.y * 2;
	d0.y = d0.y + 1.5;
	r1 = (float)d0.y;
	r0.z = asint((4 < d0.y) ? -1 : 0);
	o.z = asfloat(r0.z & 1065353216);
	o.xy = asfloat(r0.xy) + r1.xx;
	o.w = 0;

	return o;
}
