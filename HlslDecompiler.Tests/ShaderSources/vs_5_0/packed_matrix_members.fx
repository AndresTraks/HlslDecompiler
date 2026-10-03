cbuffer Params : register(b0)
{
	float4x4 viewProjection;
};

struct NodesElement
{
	float3x3 rotation;
	row_major float4x4 world;
	float4 tint;
};

StructuredBuffer<NodesElement> nodes : register(t0);

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

	float t0 = nodes[i.sv_instanceid].world[3].w;
	float t1 = nodes[i.sv_instanceid].world[2].w;
	float t2 = nodes[i.sv_instanceid].world[1].w;
	float t3 = nodes[i.sv_instanceid].world[0].w;
	float t4 = nodes[i.sv_instanceid].world[3].z;
	float t5 = nodes[i.sv_instanceid].world[2].z;
	float t6 = nodes[i.sv_instanceid].world[1].z;
	float t7 = nodes[i.sv_instanceid].world[0].z;
	float t8 = nodes[i.sv_instanceid].world[3].y;
	float t9 = nodes[i.sv_instanceid].world[2].y;
	float t10 = nodes[i.sv_instanceid].world[1].y;
	float t11 = nodes[i.sv_instanceid].world[0].y;
	float t12 = nodes[i.sv_instanceid].world[3].x;
	float t13 = nodes[i.sv_instanceid].world[2].x;
	float t14 = nodes[i.sv_instanceid].world[1].x;
	float t15 = nodes[i.sv_instanceid].world[0].x;
	o.sv_position = mul(float4(i.position.x * t15 + i.position.y * t14 + i.position.z * t13 + t12, i.position.x * t11 + i.position.y * t10 + i.position.z * t9 + t8, i.position.x * t7 + i.position.y * t6 + i.position.z * t5 + t4, i.position.x * t3 + i.position.y * t2 + i.position.z * t1 + t0), viewProjection);
	o.normal = mul(i.normal, nodes[i.sv_instanceid].rotation);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
