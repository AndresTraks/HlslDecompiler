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

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	r0 = nodes[i.sv_instanceid].world[0];
	r1.x = r0.x;
	r2 = nodes[i.sv_instanceid].world[1].xzyw;
	r1.y = r2.x;
	r3 = nodes[i.sv_instanceid].world[2].xywz;
	r1.z = r3.x;
	r4 = nodes[i.sv_instanceid].world[3];
	r1.w = r4.x;
	r5.xyz = i.position.xyz;
	r5.w = 1;
	r1.x = dot(r5, r1);
	r6.x = r0.y;
	r6.y = r2.z;
	r6.z = r3.y;
	r6.w = r4.y;
	r1.y = dot(r5, r6);
	r2.x = r0.z;
	r3.x = r0.w;
	r3.y = r2.w;
	r2.z = r3.w;
	r2.w = r4.z;
	r3.w = r4.w;
	r1.w = dot(r5, r3);
	r1.z = dot(r5, r2);
	o.sv_position.x = dot(r1, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r1, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r1, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r1, transpose(viewProjection)[3]);
	r0.xyz = transpose(nodes[i.sv_instanceid].rotation)[0];
	o.normal.x = dot(i.normal.xyz, r0.xyz);
	r0.xyz = transpose(nodes[i.sv_instanceid].rotation)[1];
	o.normal.y = dot(i.normal.xyz, r0.xyz);
	r0.xyz = transpose(nodes[i.sv_instanceid].rotation)[2];
	o.normal.z = dot(i.normal.xyz, r0.xyz);
	o.color = nodes[i.sv_instanceid].tint;

	return o;
}
