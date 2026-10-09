cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float4 planes[2];
};

struct Instance
{
	float4x4 world;
	float4 tint;
};

StructuredBuffer<Instance> instances : register(t0);

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

	float4 t0 = transpose(instances[i.sv_instanceid].world)[0];
	float t1 = dot(float4(i.position, 1), t0);
	float4 t2 = transpose(instances[i.sv_instanceid].world)[1];
	float t3 = dot(float4(i.position, 1), t2);
	float4 t4 = transpose(instances[i.sv_instanceid].world)[2];
	float t5 = dot(float4(i.position, 1), t4);
	float4 t6 = transpose(instances[i.sv_instanceid].world)[3];
	float t7 = dot(float4(i.position, 1), t6);
	o.sv_position = mul(float4(t1, t3, t5, t7), viewProjection);
	o.color = instances[i.sv_instanceid].tint;
	o.sv_clipdistance = float2(t1 * planes[0].x + t3 * planes[0].y + t5 * planes[0].z + t7 * planes[0].w, t1 * planes[1].x + t3 * planes[1].y + t5 * planes[1].z + t7 * planes[1].w);

	return o;
}
