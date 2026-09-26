cbuffer Params : register(b0)
{
	float4x4 viewProjection;
};

struct InstancesElement
{
	float4x4 world;
	float4 tint;
};

StructuredBuffer<InstancesElement> instances : register(t0);

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

	o.sv_position = mul(mul(float4(i.position, 1), instances[i.sv_instanceid].world), viewProjection);
	o.color = instances[i.sv_instanceid].tint;

	return o;
}
