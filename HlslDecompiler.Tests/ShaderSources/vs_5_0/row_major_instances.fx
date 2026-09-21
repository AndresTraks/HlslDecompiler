struct struct1
{
	row_major float4x3 bone;
	float4 tint;
};

cbuffer Instances : register(b0)
{
	struct1 instances[8];
};

struct VS_IN
{
	float4 position : POSITION;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	return float4(i.position.x * instances[i.sv_instanceid].bone[0] + i.position.y * instances[i.sv_instanceid].bone[1] + i.position.z * instances[i.sv_instanceid].bone[2] + i.position.w * instances[i.sv_instanceid].bone[3], 1) + instances[i.sv_instanceid].tint;
}
