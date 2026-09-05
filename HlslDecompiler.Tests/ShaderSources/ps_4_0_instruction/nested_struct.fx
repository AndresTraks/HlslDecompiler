struct struct1
{
	float3 v;
	float s;
};

struct struct2
{
	struct1 a;
	float4 b;
};

struct2 o;

float4 main(float3 normal : NORMAL) : SV_Target
{
	float4 o;

	float r0;
	r0 = dot(normal.xyz, o.a.v.xyz);
	o = o.b * r0.x + o.a.s.w;

	return o;
}
