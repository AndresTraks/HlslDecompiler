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
	return float4(lights[1].colour.x * saturate(dot(normal, -lights[1].dir)) + lights[0].colour.x * saturate(dot(normal, -lights[0].dir)) + ambient.x, lights[1].colour.y * saturate(dot(normal, -lights[1].dir)) + lights[0].colour.y * saturate(dot(normal, -lights[0].dir)) + ambient.y, lights[1].colour.z * saturate(dot(normal, -lights[1].dir)) + lights[0].colour.z * saturate(dot(normal, -lights[0].dir)) + ambient.z, lights[1].colour.w * saturate(dot(normal, -lights[1].dir)) + lights[0].colour.w * saturate(dot(normal, -lights[0].dir)) + 1);
}
