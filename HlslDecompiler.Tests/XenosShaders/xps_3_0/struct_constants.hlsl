struct struct1
{
	float4 dir;
	float4 col;
};

struct1 lights[3];

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float t0 = saturate(dot(lights[0].dir.xyz, texcoord));
	float t1 = saturate(dot(lights[1].dir.xyz, texcoord));
	float t2 = saturate(dot(lights[2].dir.xyz, texcoord));
	return t1 * lights[1].col + t2 * lights[2].col + t0 * lights[0].col;
}
