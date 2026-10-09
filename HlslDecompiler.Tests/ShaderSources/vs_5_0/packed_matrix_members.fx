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

	float t0 = nodes[i.sv_instanceid].world[0].w;
	float t1 = nodes[i.sv_instanceid].world[0].x;
	float t2 = nodes[i.sv_instanceid].world[0].y;
	float t3 = nodes[i.sv_instanceid].world[0].z;
	float t4 = nodes[i.sv_instanceid].world[1].w;
	float t5 = nodes[i.sv_instanceid].world[1].x;
	float t6 = nodes[i.sv_instanceid].world[1].y;
	float t7 = nodes[i.sv_instanceid].world[1].z;
	float t8 = nodes[i.sv_instanceid].world[2].w;
	float t9 = nodes[i.sv_instanceid].world[2].x;
	float t10 = nodes[i.sv_instanceid].world[2].y;
	float t11 = nodes[i.sv_instanceid].world[2].z;
	float t12 = nodes[i.sv_instanceid].world[3].w;
	float t13 = nodes[i.sv_instanceid].world[3].x;
	float t14 = nodes[i.sv_instanceid].world[3].y;
	float t15 = nodes[i.sv_instanceid].world[3].z;
	o.sv_position = mul(float4(i.position.x * t1 + i.position.y * t5 + i.position.z * t9 + t13, i.position.x * t2 + i.position.y * t6 + i.position.z * t10 + t14, i.position.x * t3 + i.position.y * t7 + i.position.z * t11 + t15, i.position.x * t0 + i.position.y * t4 + i.position.z * t8 + t12), viewProjection);
	o.normal = mul(i.normal, nodes[i.sv_instanceid].rotation);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
