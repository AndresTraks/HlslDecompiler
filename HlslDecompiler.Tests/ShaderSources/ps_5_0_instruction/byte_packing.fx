uint4 v;

float4 main() : SV_Target
{
	float4 o;

	int3 r0;
	r0.xy = (int2(0, 0) & ~(((1 << int2(8, 8)) - 1) << int2(16, 8))) | ((v.zy << int2(16, 8)) & (((1 << int2(8, 8)) - 1) << int2(16, 8)));
	r0.y = (r0.y & ~(((1 << 8) - 1) << 0)) | ((v.x << 0) & (((1 << 8) - 1) << 0));
	r0.x = r0.x + r0.y;
	r0.x = (r0.x & ~(((1 << 8) - 1) << 24)) | ((v.w << 24) & (((1 << 8) - 1) << 24));
	r0.y = r0.x & 255;
	r0.xz = ((uint2)r0.xx >> int2(8, 16)) & ((1 << int2(8, 8)) - 1);
	o.xyz = (float3)(uint3)r0.yxz;
	r0.x = v.w & 255;
	o.w = (float)(uint)r0.x;

	return o;
}
