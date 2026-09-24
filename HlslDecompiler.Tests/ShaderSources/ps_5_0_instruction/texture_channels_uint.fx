Texture2D<uint4> tu;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	r0 = tu.Load(int3(1, 2, 0));
	o = (float4)(uint4)r0.zxwy;

	return o;
}
