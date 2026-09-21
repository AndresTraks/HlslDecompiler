struct struct1
{
	row_major float4x4 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 position : POSITION) : POSITION
{
	return mul(position, g_Xform.bone) + g_Xform.tint;
}
