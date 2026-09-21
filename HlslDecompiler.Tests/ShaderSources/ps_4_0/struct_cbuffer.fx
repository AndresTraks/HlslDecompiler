struct struct1
{
	float3 dir;
	float pad;
	float4 colour;
};

cbuffer cb : register(b0)
{
	struct1 lights[2];
	float3 ambient;
};

float4 main(float3 normal : NORMAL) : SV_Target
{
	float t0 = saturate(dot(normal, -lights[1].dir));
	float t1 = saturate(dot(normal, -lights[0].dir));
	return lights[0].colour * t1 + float4(ambient, 1) + lights[1].colour * t0;
}
