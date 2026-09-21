struct struct1
{
	float4x4 world;
	row_major float4x3 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(mul(texcoord, g_Xform.bone) + mul(texcoord, (float4x3)g_Xform.world) + g_Xform.tint.xyz, dot(transpose(g_Xform.world)[3], texcoord) + g_Xform.tint.w + 1);
}
