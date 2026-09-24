Texture2D<int4> ti;

float4 main() : SV_Target
{
	return (float4)(ti.Load(int3(3, 4, 0)).ywxz);
}
