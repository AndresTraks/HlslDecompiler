struct struct1
{
	float4x4 world;
	row_major float4x3 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(texcoord.x * g_Xform.bone[0].x + g_Xform.bone[1].x * texcoord.y + texcoord.z * g_Xform.bone[2].x + texcoord.w * g_Xform.bone[3].x + dot(transpose(g_Xform.world)[0], texcoord) + g_Xform.tint.x, texcoord.x * g_Xform.bone[0].y + g_Xform.bone[1].y * texcoord.y + texcoord.z * g_Xform.bone[2].y + texcoord.w * g_Xform.bone[3].y + dot(transpose(g_Xform.world)[1], texcoord) + g_Xform.tint.y, texcoord.x * g_Xform.bone[0].z + g_Xform.bone[1].z * texcoord.y + texcoord.z * g_Xform.bone[2].z + texcoord.w * g_Xform.bone[3].z + dot(transpose(g_Xform.world)[2], texcoord) + g_Xform.tint.z, dot(transpose(g_Xform.world)[3], texcoord) + g_Xform.tint.w + 1);
}
