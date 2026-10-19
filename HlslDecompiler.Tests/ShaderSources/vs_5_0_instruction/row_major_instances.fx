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
	float4 o;

	int4 r0;
	float4 r1;
	r0.x = i.sv_instanceid * 5;
	r0.yzw = asint(i.position.yyy * instances[r0.x / 5].bone[1].xyz);
	r0.yzw = asint(i.position.xxx * instances[r0.x / 5].bone[0].xyz + asfloat(r0.yzw));
	r0.yzw = asint(i.position.zzz * instances[r0.x / 5].bone[2].xyz + asfloat(r0.yzw));
	r1.xyz = i.position.www * instances[r0.x / 5].bone[3].xyz + asfloat(r0.yzw);
	r1.w = 1;
	o = r1 + instances[r0.x / 5].tint;

	return o;
}
