float4 main(nointerpolation uint2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float r1;
	r0.xy = (uint2)texcoord.xy >> uint2(16, 16);
	r0.zw = f16tof32(r0.xy);
	r0.xy = f16tof32(texcoord.xy);
	r1 = r0.w + r0.x;
	r1 = f32tof16(r1.x);
	r1 = (float)(uint)r1.x;
	o = r0 + r1.x;

	return o;
}
