tbuffer tb : register(t0)
{
	float4x4 boneMats[16];
	float4 tint;
};

struct VS_IN
{
	float3 position : POSITION;
	uint2 blendindices : BLENDINDICES;
	float2 blendweight : BLENDWEIGHT;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = float4(dot(float4(i.position, 1), transpose(boneMats[i.blendindices.x])[0]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.x])[1]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.x])[2]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.x])[3])) * i.blendweight.x + float4(dot(float4(i.position, 1), transpose(boneMats[i.blendindices.y])[0]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.y])[1]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.y])[2]), dot(float4(i.position, 1), transpose(boneMats[i.blendindices.y])[3])) * i.blendweight.y;
	o.color = tint;

	return o;
}
