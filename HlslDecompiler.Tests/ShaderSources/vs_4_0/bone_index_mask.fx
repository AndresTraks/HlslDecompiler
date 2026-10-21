cbuffer C : register(b0)
{
	float4x4 bones[8];
};

struct VS_IN
{
	float4 position : POSITION;
	int4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	int t0 = i.blendindices.w + i.blendindices.z;
	return mul(i.position, bones[i.blendindices.x]) * i.blendweight.x + mul(i.position, bones[(i.blendindices.y & 7)]) * i.blendweight.y + mul(i.position, bones[t0 & -2147483648 ? -(abs(t0) & 7) : abs(t0) & 7]) * i.blendweight.z;
}
