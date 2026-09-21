struct struct1
{
	float3 position;
	float range;
	float4 color;
};

int g_Index : register(c24);
struct1 g_Lights[8];

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	int a0;
	float4 r1;
	r0.x = 3;
	r0.x = r0.x * g_Index.x;
	a0 = r0.x;
	r0 = g_Lights[a0 / 3].color;
	r1.x = g_Lights[a0 / 3].range;
	r1.xyz = r0.xyz * r1.xxx + g_Lights[a0 / 3].position.xyz;
	r1.w = r0.w * g_Lights[a0 / 3].range + position.w;
	r0 = position.xyzx * float4(1, 1, 1, 0) + float4(0, 0, 0, 1);
	o = r0 + r1;

	return o;
}
