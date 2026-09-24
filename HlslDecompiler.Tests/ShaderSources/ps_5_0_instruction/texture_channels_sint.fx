Texture2D<int4> ti;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	r0 = ti.Load(int3(3, 4, 0));
	o = (float4)r0.ywxz;

	return o;
}
