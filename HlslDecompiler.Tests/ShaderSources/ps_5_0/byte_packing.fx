uint4 v;

float4 main() : SV_Target
{
	uint t0 = ((((v.z << 16) & 16711680)) + (((((v.y << 8) & 65280)) & ~255) | (v.x & 255)) & ~-16777216) | ((v.w << 24) & -16777216);
	return float4((float)(t0 & 255), (float2)((t0 >> int2(8, 16)) & 255), (float)(v.w & 255));
}
