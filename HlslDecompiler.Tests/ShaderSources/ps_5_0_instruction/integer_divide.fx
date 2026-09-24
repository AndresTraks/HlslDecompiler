int4 a;
int4 b;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	int4 r3;
	r0 = a ^ b;
	r0 = r0 & int4(-2147483648, -2147483648, -2147483648, -2147483648);
	r1 = max(a, -(a));
	r2 = max(b, -(b));
	uint4 dividend0 = (uint4)r1;
	r1 = dividend0 / (uint4)r2;
	r2 = dividend0 % (uint4)r2;
	r3 = -r1;
	r0 = (r0 != 0) ? r3 : r1;
	r0 = asint((float4)r0);
	r1 = -r2;
	r3 = a & int4(-2147483648, -2147483648, -2147483648, -2147483648);
	r1 = (r3 != 0) ? r1 : r2;
	r1 = asint((float4)r1);
	o = asfloat(r0) + asfloat(r1);

	return o;
}
