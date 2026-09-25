float4x4 viewProjection;

StructuredBuffer<float4x4> instances : register(t0);

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
	uint sv_instanceid : SV_InstanceID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	r0 = transpose(instances[i.sv_instanceid])[3];
	r0.w = dot(i.position, r0);
	r1 = transpose(instances[i.sv_instanceid])[0];
	r0.x = dot(i.position, r1);
	o.normal.x = dot(i.normal.xyz, r1.xyz);
	r1 = transpose(instances[i.sv_instanceid])[1];
	r0.y = dot(i.position, r1);
	o.normal.y = dot(i.normal.xyz, r1.xyz);
	r1 = transpose(instances[i.sv_instanceid])[2];
	r0.z = dot(i.position, r1);
	o.normal.z = dot(i.normal.xyz, r1.xyz);
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);
	o.texcoord = r0.xyz;

	return o;
}
