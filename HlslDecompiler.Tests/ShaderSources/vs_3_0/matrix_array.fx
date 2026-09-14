float4x4 bones[4];
int idx;

float4 main(float4 position : POSITION) : POSITION
{
	return mul(position, bones[idx]);
}
