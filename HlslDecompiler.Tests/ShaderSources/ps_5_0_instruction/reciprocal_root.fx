float4 a;

float4 main() : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = a.y + 2;
	o.y = rcp(r0.x);
	o.z = exp2(a.z);
	r0 = abs(a.xw) + float2(1, 1);
	o.x = rsqrt(r0.x);
	o.w = log2(r0.y);

	return o;
}
