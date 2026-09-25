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
	float4 o_;

	float3 r0;
	o_.w = o.c.w;
	r0 = float3(o.i.a, o.i.b) + o.c.xyz;
	o_.xy = r0.xy + o.d.xy;
	o_.z = r0.z;

	return o_;
}
