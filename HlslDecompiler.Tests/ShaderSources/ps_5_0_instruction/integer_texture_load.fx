Texture2D<uint4> tu;
Texture2D<int4> ti;

float4 main() : SV_Target
{
	float4 o;

	int4 r0;
	o.w = 1;
	r0.xy = tu.Load(int3(1, 2, 0)).xz;
	r0.zw = ti.Load(int3(3, 4, 0)).yw;
	r0.x = r0.z + r0.x;
	o.z = (float)r0.w;
	o.xy = (float2)(uint2)r0.xy;

	return o;
}
