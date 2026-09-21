struct struct1
{
	float4x4 world;
	float4 tint;
};

int g_Index : register(c20);
struct1 g_Instances[4];

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	int a0;
	r0.x = g_Index.x;
	r0.x = r0.x * 5;
	a0 = r0.x;
	r0.x = dot(position, transpose(g_Instances[a0 / 5].world)[0]);
	r0.y = dot(position, transpose(g_Instances[a0 / 5].world)[1]);
	r0.z = dot(position, transpose(g_Instances[a0 / 5].world)[2]);
	r0.w = dot(position, transpose(g_Instances[a0 / 5].world)[3]);
	o = r0 + g_Instances[a0 / 5].tint;

	return o;
}
