row_major float4x4 world;
float4x4 view;

float4 main(float4 position : POSITION) : POSITION
{
	return mul(mul(position, world), view);
}
