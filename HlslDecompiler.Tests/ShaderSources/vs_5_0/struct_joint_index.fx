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
	int4 t0 = i.joints * 4;
	float3 t1 = mul(sk.joints[t0.x / 4].m, float4(i.position, 1)) * i.weights.x * sk.joints[t0.x / 4].weight + mul(sk.joints[t0.y / 4].m, float4(i.position, 1)) * i.weights.y * sk.joints[t0.y / 4].weight + mul(sk.joints[t0.z / 4].m, float4(i.position, 1)) * i.weights.z * sk.joints[t0.z / 4].weight + mul(sk.joints[t0.w / 4].m, float4(i.position, 1)) * i.weights.w * sk.joints[t0.w / 4].weight;
	return mul(float4(t1, 1), vp) * sk.tint.w;
}
