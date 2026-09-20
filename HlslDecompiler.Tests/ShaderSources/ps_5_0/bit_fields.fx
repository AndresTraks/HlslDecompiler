uint packed;
int signedPacked;

float4 main() : SV_Target
{
	return float4((float2)((packed >> int2(3, 12)) & int2(31, 255)), (float)((signedPacked << 20) >> 24), (float)((packed & ~65280) | ((signedPacked << 8) & 65280)));
}
