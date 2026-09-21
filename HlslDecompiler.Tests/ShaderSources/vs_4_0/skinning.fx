cbuffer cb : register(b0)
{
	float4x4 bones[4];
	float4x4 viewProj;
};

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	return mul(mul(i.position, bones[0]) * i.blendweight.x + mul(i.position, bones[1]) * i.blendweight.y, viewProj);
}
