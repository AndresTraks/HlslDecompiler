float4 a;

float4 main() : SV_Target
{
	return float4(rsqrt(abs(a.x) + 1), rcp(2 + a.y), exp2(a.z), log2(abs(a.w) + 1));
}
