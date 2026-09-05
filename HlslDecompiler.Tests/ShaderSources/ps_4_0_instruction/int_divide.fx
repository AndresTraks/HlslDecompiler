int a;
int b;

float4 main() : SV_Target
{
	float4 o;

	float3 r0;
	r0.x = b ^ a;
	r0.x = r0.x & -2.1474836E+09;
	r0.yz = max(a, -(a));
	r1 = r0.y / r0.z;
	r2 = r0.y % r0.z;
	r0.y = -r1.x;
	r0.x = (r0.x != 0) ? r0.y : r1.x;
	r0.y = -r2.x;
	r0.z = a & -2.1474836E+09;
	r0.y = (r0.z != 0) ? r0.y : r2.x;
	r0.xy = r0.xy;
	o = r0.y + r0.x;

	return o;
}
