float4 main(float4 c : C) : SV_Target
{
	float4 o;

	int4 r0;
	r0.x = (uint)asint(c.w) >> 30;
	switch (r0.x) {
		case 0:
		r0 = asint(c);
		break;
		case 1:
		r0 = asint(c + c);
		break;
		case 2:
		r0 = asint(c) ^ int4(-2147483648, -2147483648, -2147483648, -2147483648);
		break;
		default:
		r0 = int4(1065353216, 1065353216, 1065353216, 1065353216);
		break;
	}
	o = asfloat(r0);

	return o;
}
