Texture2D<uint4> tu;

float4 main() : SV_Target
{
	return (float4)(tu.Load(int3(1, 2, 0)).zxwy);
}
