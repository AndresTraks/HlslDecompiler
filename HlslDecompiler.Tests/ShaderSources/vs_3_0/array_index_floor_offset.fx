float4x4 bones[10];
float4 col[16];

struct VS_IN
{
	float4 position : POSITION;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	return mul(i.position, bones[2 + i.blendindices.x]) + col[2 * i.blendindices.y + 1];
}
