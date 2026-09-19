float4x4 view : register(c4);
row_major float4x4 world;

float4 main(float4 position : POSITION) : POSITION
{
	return mul(mul(position, world), view);
}
