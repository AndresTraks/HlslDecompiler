uint mask;
uint4 packed;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float2 t0 = f16tof32(packed.xy);
	return float4((float)(countbits(mask) + firstbithigh(mask) + firstbitlow(mask)), (float)(reversebits(mask) & 255), t0.y + t0.x, (float)(msad4(packed.x, uint2(packed.y, packed.z), uint4(packed.x, 0, 0, 0)).x + msad4(packed.x, uint2(packed.y, packed.z), uint4(0, 0, 0, packed.w)).w));
}
