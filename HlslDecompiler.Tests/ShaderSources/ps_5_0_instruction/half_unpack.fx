float4 main(nointerpolation uint2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int r1;
	r0.xy = (uint2)texcoord.xy >> uint2(16, 16);
	r0.zw = asint(f16tof32(r0.xy));
	r0.xy = asint(f16tof32(texcoord.xy));
	r1 = asint(asfloat(r0.w) + asfloat(r0.x));
	r1 = f32tof16(asfloat(r1.x));
	r1 = asint((float)(uint)r1.x);
	o = asfloat(r0) + asfloat(r1.x);

	return o;
}
