float4 main(float4 c : C) : SV_Target
{
	int t0;
	float2 t1;
	int t2;
	switch ((uint)asint(c.w) >> 30) {
		case 0:
			t0 = asint(c.x);
			t1 = c.yz;
			t2 = asint(c.w);
			break;
		case 1:
			t0 = asint(2 * c.x);
			t1 = 2 * c.yz;
			t2 = asint(2 * c.w);
			break;
		case 2:
			t0 = asint(c.x) ^ -2147483648;
			t1 = asfloat(asint(c.yz) ^ -2147483648);
			t2 = asint(c.w) ^ -2147483648;
			break;
		default:
			t0 = 1065353216;
			t1 = 1;
			t2 = 1065353216;
			break;
	}
	return float4(asfloat(t0), t1, asfloat(t2));
}
