struct struct1
{
	float3 position;
	float range;
	float4 color;
};

struct1 g_PointLight[2];

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float3 r1;
	r0.xyz = g_PointLight[1].position.xyz + -texcoord.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = 1 / r0.x;
	r1 = g_PointLight[1].color.xyz;
	r0.yzw = r1.xyz * g_PointLight[1].range;
	o.xyz = r0.xxx * r0.yzw;
	o.w = 1;

	return o;
}
