uint4 v;

float4 main() : SV_Target
{
	return float4((float3)(((v.xyz << int3(2, 8, 4)) & int3(28, 65280, 240))), (float)v.w);
}
