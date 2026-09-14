struct struct1
{
	float3 dir;
	float pad;
	float4 colour;
};

struct1 lights[2];
float3 ambient;

float4 main(float3 normal : NORMAL) : SV_Target
{
	return lights[1].colour * saturate(dot(normal, -lights[1].dir)) + lights[0].colour * saturate(dot(normal, -lights[0].dir)) + float4(ambient, 1);
}
