cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float4 planes[2];
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
	float2 sv_clipdistance : SV_ClipDistance;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = transpose(instances[i.sv_instanceid].world)[0];
	r1.xyz = i.position.xyz;
	r1.w = 1;
	r0.x = dot(r1, r0);
	r2 = transpose(instances[i.sv_instanceid].world)[1];
	r0.y = dot(r1, r2);
	r2 = transpose(instances[i.sv_instanceid].world)[2];
	r0.z = dot(r1, r2);
	r2 = transpose(instances[i.sv_instanceid].world)[3];
	r0.w = dot(r1, r2);
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);
	o.color = instances[i.sv_instanceid].tint;
	o.sv_clipdistance.x = dot(r0, planes[0]);
	o.sv_clipdistance.y = dot(r0, planes[1]);

	return o;
}
