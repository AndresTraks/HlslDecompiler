struct struct1
{
	row_major float4x4 bone;
	float4 tint;
};

struct1 g_Xform;

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	r0 = position.y * g_Xform.bone[1];
	r0 = position.x * g_Xform.bone[0] + r0;
	r0 = position.z * g_Xform.bone[2] + r0;
	r0 = position.w * g_Xform.bone[3] + r0;
	o = r0 + g_Xform.tint;

	return o;
}
