uint mask;
uint4 packed;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int2 r0;
	r0.x = mask != 0 ? 31 - firstbithigh(mask) : -1;
	r0.x = -(r0.x) + 31;
	r0.x = (mask != 0) ? r0.x : -1;
	r0.y = countbits(mask);
	r0.x = r0.x + r0.y;
	r0.y = firstbitlow(mask);
	r0.x = r0.y + r0.x;
	o.x = (float)(uint)r0.x;
	r0.x = (uint)packed.y >> 24;
	r0.y = (r0.x & ~(((1 << 24) - 1) << 8)) | ((packed.z << 8) & (((1 << 24) - 1) << 8));
	r0.x = packed.y;
	r0 = uint2(msad4(packed.x, uint2(r0.x, 0), uint4(packed.x, 0, 0, 0)).x, msad4(packed.x, uint2(r0.y, 0), uint4(packed.w, 0, 0, 0)).x);
	r0.x = r0.y + r0.x;
	o.w = (float)(uint)r0.x;
	r0.x = reversebits(mask);
	r0.x = r0.x & 255;
	o.y = (float)(uint)r0.x;
	r0 = asint(f16tof32(packed.xy));
	o.z = asfloat(r0.y) + asfloat(r0.x);

	return o;
}
