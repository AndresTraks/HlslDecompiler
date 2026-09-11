float4x4 viewProj;
float4x4 instances[8];

struct VS_IN
{
	float4 position : POSITION;
	uint sv_instanceid : SV_InstanceID;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	int r0;
	float4 r1;
	r0 = i.sv_instanceid.x << 2;
	r1.x = dot(i.position, transpose(instances[r0.x / 4])[0]);
	r1.y = dot(i.position, transpose(instances[r0.x / 4])[1]);
	r1.z = dot(i.position, transpose(instances[r0.x / 4])[2]);
	r1.w = dot(i.position, transpose(instances[r0.x / 4])[3]);
	o.x = dot(r1, transpose(viewProj)[0]);
	o.y = dot(r1, transpose(viewProj)[1]);
	o.z = dot(r1, transpose(viewProj)[2]);
	o.w = dot(r1, transpose(viewProj)[3]);

	return o;
}
