float4x4 world;
float4x4 viewProj;

float4 main(float4 position : POSITION) : SV_Position
{
	return mul(mul(position, world), viewProj);
}
