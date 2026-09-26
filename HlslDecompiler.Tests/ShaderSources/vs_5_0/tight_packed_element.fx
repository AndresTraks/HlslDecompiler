cbuffer Params : register(b0)
{
	float4x4 viewProjection;
};

struct NodesElement
{
	float3x3 rotation;
	float4 tint;
	float3 offset;
};

StructuredBuffer<NodesElement> nodes : register(t0);

struct VS_IN
{
	float3 position : POSITION;
	uint sv_instanceid : SV_InstanceID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = mul(float4(nodes[i.sv_instanceid].offset + i.position, 1), viewProjection);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
