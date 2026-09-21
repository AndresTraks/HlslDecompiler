struct struct1
{
	float4x4 world;
	row_major float4x3 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(texcoord.x * g_Xform.bone[0] + g_Xform.bone[1] * texcoord.y + texcoord.z * g_Xform.bone[2] + texcoord.w * g_Xform.bone[3] + mul(texcoord, (float4x3)g_Xform.world) + g_Xform.tint.xyz, dot(transpose(g_Xform.world)[3], texcoord) + g_Xform.tint.w + 1);
}
