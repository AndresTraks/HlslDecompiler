float4 main(nointerpolation uint2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = f16tof32(texcoord);
	float2 t1 = f16tof32(texcoord >> 16);
	float t2 = (float)f32tof16(t1.y + t0.x);
	return float4(t0, t1) + t2;
}
