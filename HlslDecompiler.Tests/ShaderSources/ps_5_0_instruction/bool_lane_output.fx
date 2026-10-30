cbuffer c : register(b0)
{
	float4 v;
	float4 d;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	int4 r2;
	r0 = asint(texcoord / d);
	r1 = r0 & int4(2147483647, 2147483647, 2147483647, 2147483647);
	r1 = (r1 == int4(2139095040, 2139095040, 2139095040, 2139095040)) ? -1 : 0;
	r2 = (asfloat(r0) != asfloat(r0)) ? -1 : 0;
	r1 = r1 | r2;
	r2.xy = r1.zw | r1.xy;
	r0 = (r1 != 0) ? asint(v) : r0;
	r1.x = r2.y | r2.x;
	r1.x = r1.x & 1;
	o.x = (float)r1.x;
	o.z = asfloat(r0.y) + asfloat(r0.x);
	o.w = max(asfloat(r0.w), asfloat(r0.z));
	o.y = 1;

	return o;
}
