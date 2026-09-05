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
	return float4(o.b.x * dot(o.a.v, normal) + o.a.s, o.b.y * dot(o.a.v, normal) + o.a.s, o.b.z * dot(o.a.v, normal) + o.a.s, o.b.w * dot(o.a.v, normal) + o.a.s);
}
