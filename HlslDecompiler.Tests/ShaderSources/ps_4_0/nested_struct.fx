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
	float t0 = dot(o.a.v, normal);
	return o.b * t0 + o.a.s;
}
