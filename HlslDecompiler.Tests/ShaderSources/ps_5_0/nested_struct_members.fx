struct struct1
{
	float2 a;
	float b;
};

struct struct2
{
	struct1 i;
	float4 c;
	float2 d;
};

cbuffer CB : register(b0)
{
	struct2 o;
};

float4 main() : SV_Target
{
	return float4(o.i.a + o.c.xy + o.d, o.i.b + o.c.z, o.c.w);
}
