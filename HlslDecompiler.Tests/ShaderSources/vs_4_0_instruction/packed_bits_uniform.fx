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
	float4 o;

	float3 r0;
	r0 = i.position.xyz * i.blendweight;
	o.xyz = r0.xyz * packedScale + offset;
	o.w = 1;

	return o;
}
