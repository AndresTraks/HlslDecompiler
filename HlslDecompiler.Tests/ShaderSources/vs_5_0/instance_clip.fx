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

	float t0 = dot(float4(i.position, 1), transpose(instances[i.sv_instanceid].world)[0]);
	float t1 = dot(float4(i.position, 1), transpose(instances[i.sv_instanceid].world)[1]);
	float t2 = dot(float4(i.position, 1), transpose(instances[i.sv_instanceid].world)[2]);
	float t3 = dot(float4(i.position, 1), transpose(instances[i.sv_instanceid].world)[3]);
	o.sv_position = mul(float4(t0, t1, t2, t3), viewProjection);
	o.color = instances[i.sv_instanceid].tint;
	o.sv_clipdistance = float2(t0 * planes[0].x + t1 * planes[0].y + t2 * planes[0].z + t3 * planes[0].w, t0 * planes[1].x + t1 * planes[1].y + t2 * planes[1].z + t3 * planes[1].w);

	return o;
}
