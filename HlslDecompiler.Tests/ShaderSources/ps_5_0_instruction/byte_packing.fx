uint4 v;

float4 main() : SV_Target
{
	float4 o;

	int3 r0;
	r0.xy = (int2(0, 0) & ~(int2(255, 255) << int2(16, 8))) | ((v.zy << int2(16, 8)) & (int2(255, 255) << int2(16, 8)));
	r0.y = (r0.y & ~255) | (v.x & 255);
	r0.x = r0.x + r0.y;
	r0.x = (r0.x & ~(255 << 24)) | ((v.w << 24) & (255 << 24));
	r0.y = r0.x & 255;
	r0.xz = ((uint2)r0.xx >> int2(8, 16)) & int2(255, 255);
	o.xyz = (float3)(uint3)r0.yxz;
	r0.x = v.w & 255;
	o.w = (float)(uint)r0.x;

	return o;
}
