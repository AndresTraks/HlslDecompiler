Texture2D<uint4> tu;
Texture2D<int4> ti;

float4 main() : SV_Target
{
	return float4((float2)uint2(ti.Load(uint3(3, 4, 0)).y + tu.Load(uint3(1, 2, 0)).x, tu.Load(uint3(1, 2, 0)).z), (float)(ti.Load(int3(3, 4, 0)).w), 1);
}
