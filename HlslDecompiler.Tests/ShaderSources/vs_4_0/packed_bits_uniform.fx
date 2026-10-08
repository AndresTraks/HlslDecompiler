cbuffer Packed : register(b0)
{
	uint packedScale;
	float offset;
};

struct VS_IN
{
	float3 position : POSITION;
	uint blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	return float4(i.position * asfloat(i.blendweight) * asfloat(packedScale) + offset, 1);
}
