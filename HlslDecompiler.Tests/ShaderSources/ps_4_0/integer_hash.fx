uint seed;
uint count;

float4 main() : SV_Target
{
	int t0 = seed;
	for (int t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = ((uint)t0 >> 16) ^ 1664525 * t0 + 1013904223;
	}
	return float4(0.00392156886 * (float)(t0 & 255), 0.00392156886 * (float2)(((uint2)t0 >> int2(8, 16)) & 255), 1);
}
