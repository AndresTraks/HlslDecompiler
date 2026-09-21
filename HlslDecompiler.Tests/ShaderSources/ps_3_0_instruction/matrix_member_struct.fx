struct struct1
{
	float4x4 world;
	row_major float4x3 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float3 r0;
	float3 r1;
	r0 = g_Xform.bone[1].xyz * texcoord.yyy;
	r0 = texcoord.xxx * g_Xform.bone[0].xyz + r0.xyz;
	r0 = texcoord.zzz * g_Xform.bone[2].xyz + r0.xyz;
	r0 = texcoord.www * g_Xform.bone[3].xyz + r0.xyz;
	r1.x = dot(texcoord, transpose(g_Xform.world)[0]);
	r1.y = dot(texcoord, transpose(g_Xform.world)[1]);
	r1.z = dot(texcoord, transpose(g_Xform.world)[2]);
	r0 = r0.xyz + r1.xyz;
	o.xyz = r0.xyz + g_Xform.tint.xyz;
	r0.x = dot(texcoord, transpose(g_Xform.world)[3]);
	r0.x = r0.x + g_Xform.tint.w;
	o.w = r0.x + 1;

	return o;
}
