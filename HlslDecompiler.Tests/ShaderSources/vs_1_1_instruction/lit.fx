float4 ambient : register(c1);
float4 v;

float4 main() : POSITION
{
	float4 o;

	float4 r0;
	r0 = lit(v.xyzz.x, v.xyzz.y, v.xyzz.w);
	o = ambient * r0.y + r0.z;

	return o;
}
