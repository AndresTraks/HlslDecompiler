float4x4 viewProj;
float4x4 instances[8];

struct VS_IN
{
	float4 position : POSITION;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	return mul(float4(dot(i.position, transpose(instances[i.sv_instanceid])[0]), dot(i.position, transpose(instances[i.sv_instanceid])[1]), dot(i.position, transpose(instances[i.sv_instanceid])[2]), dot(i.position, transpose(instances[i.sv_instanceid])[3])), viewProj);
}
