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
	float4 o;

	float2 r0;
	float4 r1;
	r0.x = dot(normal.xyz, -(lights[1].dir.xyz));
	r0.y = dot(normal.xyz, -(lights[0].dir.xyz));
	r1.xyz = lights[0].colour.xyz * r0.yyy + ambient.xyz;
	r1.w = lights[0].colour.w * r0.y + 1;
	o = lights[1].colour * r0.x + r1;

	return o;
}
