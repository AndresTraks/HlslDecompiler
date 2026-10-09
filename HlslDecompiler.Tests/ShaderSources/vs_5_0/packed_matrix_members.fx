cbuffer Params : register(b0)
{
	float4x4 viewProjection;
};

struct Node
{
	float3x3 rotation;
	row_major float4x4 world;
	float4 tint;
};

StructuredBuffer<Node> nodes : register(t0);

struct VS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	uint sv_instanceid : SV_InstanceID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 t0 = nodes[i.sv_instanceid].world[0];
	float4 t1 = nodes[i.sv_instanceid].world[1].xzyw;
	float4 t2 = nodes[i.sv_instanceid].world[2].xywz;
	float4 t3 = nodes[i.sv_instanceid].world[3];
	o.sv_position = mul(i.position.x * t0 + i.position.y * t1.xzyw + i.position.z * t2.xywz + t3, viewProjection);
	o.normal = mul(i.normal, nodes[i.sv_instanceid].rotation);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
