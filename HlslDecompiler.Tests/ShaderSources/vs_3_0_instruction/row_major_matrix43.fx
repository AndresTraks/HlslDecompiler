row_major float4x3 g_Bones[3];
row_major float4x3 g_World;

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float3 r0;
	float3 r1;
	r0 = g_World[1].xyz * position.yyy;
	r0 = position.xxx * g_World[0].xyz + r0.xyz;
	r0 = position.zzz * g_World[2].xyz + r0.xyz;
	r0 = position.www * g_World[3].xyz + r0.xyz;
	r1 = g_Bones[2][1].xyz * position.yyy;
	r1 = position.xxx * g_Bones[2][0].xyz + r1.xyz;
	r1 = position.zzz * g_Bones[2][2].xyz + r1.xyz;
	r1 = position.www * g_Bones[2][3].xyz + r1.xyz;
	o.xyz = r0.xyz + r1.xyz;
	o.w = 1;

	return o;
}
