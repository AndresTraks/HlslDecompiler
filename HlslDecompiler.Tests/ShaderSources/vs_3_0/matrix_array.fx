float4x4 bones[4];
int idx;

float4 main(float4 position : POSITION) : POSITION
{
	return float4(dot(position, transpose(bones[idx])[0]), dot(position, transpose(bones[idx])[1]), dot(position, transpose(bones[idx])[2]), dot(position, transpose(bones[idx])[3]));
}
