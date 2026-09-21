struct struct1
{
	float4x4 world;
	float4 tint;
	float scale;
};

cbuffer Instances : register(b0)
{
	struct1 instances[8];
	float4x4 viewProj;
};

struct VS_IN
{
	float4 position : POSITION;
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

	int r0;
	float4 r1;
	float4 r2;
	r0 = i.sv_instanceid.x * 6;
	r1 = i.position * instances[r0.x / 6].scale;
	r2.x = dot(r1, transpose(instances[r0.x / 6].world)[0]);
	r2.y = dot(r1, transpose(instances[r0.x / 6].world)[1]);
	r2.z = dot(r1, transpose(instances[r0.x / 6].world)[2]);
	r2.w = dot(r1, transpose(instances[r0.x / 6].world)[3]);
	o.color = instances[r0.x / 6].tint;
	o.sv_position.x = dot(r2, transpose(viewProj)[0]);
	o.sv_position.y = dot(r2, transpose(viewProj)[1]);
	o.sv_position.z = dot(r2, transpose(viewProj)[2]);
	o.sv_position.w = dot(r2, transpose(viewProj)[3]);

	return o;
}
