struct Joint
{
	row_major float3x4 m;
	float weight;
};

struct Skel
{
	Joint joints[16];
	float4 tint;
};

cbuffer S : register(b0)
{
	Skel sk;
	float4x4 vp;
};

struct VS_IN
{
	float3 position : POSITION;
	uint4 joints : JOINTS;
	float4 weights : WEIGHTS;
};

float4 main(VS_IN i) : SV_Position
{
	float4 o;

	float4 r0;
	int4 r1;
	float3 r2;
	float3 r3;
	r0.xyz = i.position.xyz;
	r0.w = 1;
	r1 = i.joints << int4(2, 2, 2, 2);
	r2.x = dot(sk.joints[r1.y / 4].m[0], r0);
	r2.y = dot(sk.joints[r1.y / 4].m[1], r0);
	r2.z = dot(sk.joints[r1.y / 4].m[2], r0);
	r2 = r2.xyz * i.weights.yyy;
	r2 = r2.xyz * sk.joints[r1.y / 4].weight;
	r3.x = dot(sk.joints[r1.x / 4].m[0], r0);
	r3.y = dot(sk.joints[r1.x / 4].m[1], r0);
	r3.z = dot(sk.joints[r1.x / 4].m[2], r0);
	r3 = r3.xyz * i.weights.xxx;
	r2 = r3.xyz * sk.joints[r1.x / 4].weight + r2.xyz;
	r3.x = dot(sk.joints[r1.z / 4].m[0], r0);
	r3.y = dot(sk.joints[r1.z / 4].m[1], r0);
	r3.z = dot(sk.joints[r1.z / 4].m[2], r0);
	r3 = r3.xyz * i.weights.zzz;
	r1.xyz = asint(r3.xyz * sk.joints[r1.z / 4].weight + r2.xyz);
	r2.x = dot(sk.joints[r1.w / 4].m[0], r0);
	r2.y = dot(sk.joints[r1.w / 4].m[1], r0);
	r2.z = dot(sk.joints[r1.w / 4].m[2], r0);
	r0.xyz = r2.xyz * i.weights.www;
	r0.xyz = r0.xyz * sk.joints[r1.w / 4].weight + asfloat(r1.xyz);
	r0.w = 1;
	r1.x = asint(dot(r0, transpose(vp)[0]));
	r1.y = asint(dot(r0, transpose(vp)[1]));
	r1.z = asint(dot(r0, transpose(vp)[2]));
	r1.w = asint(dot(r0, transpose(vp)[3]));
	o = asfloat(r1) * sk.tint.w;

	return o;
}
