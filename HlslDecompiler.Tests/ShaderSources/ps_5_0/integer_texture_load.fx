Texture2D<uint4> tu;
Texture2D<int4> ti;

float4 main() : SV_Target
{
	return float4((float)(ti.Load(int3(3, 4, 0)).y + tu.Load(int3(1, 2, 0)).x), (float)(tu.Load(int3(1, 2, 0)).z), (float)(ti.Load(int3(3, 4, 0)).w), 1);
}
