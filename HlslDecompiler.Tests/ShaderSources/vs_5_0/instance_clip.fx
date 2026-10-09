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

	float t0 = transpose(instances[i.sv_instanceid].world)[0].w;
	float t1 = transpose(instances[i.sv_instanceid].world)[0].x;
	float t2 = transpose(instances[i.sv_instanceid].world)[0].y;
	float t3 = transpose(instances[i.sv_instanceid].world)[0].z;
	float t4 = dot(float4(i.position, 1), float4(t1, t2, t3, t0));
	float t5 = transpose(instances[i.sv_instanceid].world)[1].w;
	float t6 = transpose(instances[i.sv_instanceid].world)[1].x;
	float t7 = transpose(instances[i.sv_instanceid].world)[1].y;
	float t8 = transpose(instances[i.sv_instanceid].world)[1].z;
	float t9 = dot(float4(i.position, 1), float4(t6, t7, t8, t5));
	float t10 = transpose(instances[i.sv_instanceid].world)[2].w;
	float t11 = transpose(instances[i.sv_instanceid].world)[2].x;
	float t12 = transpose(instances[i.sv_instanceid].world)[2].y;
	float t13 = transpose(instances[i.sv_instanceid].world)[2].z;
	float t14 = dot(float4(i.position, 1), float4(t11, t12, t13, t10));
	float t15 = transpose(instances[i.sv_instanceid].world)[3].w;
	float t16 = transpose(instances[i.sv_instanceid].world)[3].x;
	float t17 = transpose(instances[i.sv_instanceid].world)[3].y;
	float t18 = transpose(instances[i.sv_instanceid].world)[3].z;
	float t19 = dot(float4(i.position, 1), float4(t16, t17, t18, t15));
	o.sv_position = mul(float4(t4, t9, t14, t19), viewProjection);
	o.color = instances[i.sv_instanceid].tint;
	o.sv_clipdistance = float2(t4 * planes[0].x + t9 * planes[0].y + t14 * planes[0].z + t19 * planes[0].w, t4 * planes[1].x + t9 * planes[1].y + t14 * planes[1].z + t19 * planes[1].w);

	return o;
}
