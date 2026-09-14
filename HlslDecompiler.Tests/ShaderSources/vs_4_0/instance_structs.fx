struct struct1
{
	float4x4 world;
	float4 tint;
	float scale;
};

struct1 instances[8];
float4x4 viewProj;

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

	float4 t0 = mul(i.position * instances[i.sv_instanceid].scale, instances[i.sv_instanceid].world);
	o.sv_position = mul(t0, viewProj);
	o.color = instances[i.sv_instanceid].tint;

	return o;
}
