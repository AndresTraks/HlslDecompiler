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

	float4 r0;
	r0.xyz = nodes[i.sv_instanceid].offset;
	r0.xyz = r0.xyz + i.position.xyz;
	r0.w = 1;
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
