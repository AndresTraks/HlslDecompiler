cbuffer C : register(b0)
{
	float4x4 vp;
	float weight;
	uint vertexCount;
};

tbuffer Palette : register(t0)
{
	float4x3 bones[64];
};

Buffer<float4> deltas : register(t1);

struct VS_IN
{
	float3 position : POSITION;
	uint4 bones : BONES;
	float4 weights : WEIGHTS;
	uint sv_vertexid : SV_VertexID;
};

float4 main(VS_IN i) : SV_Position
{
	float3 t0 = deltas.Load(i.sv_vertexid).xyz * weight + i.position + deltas.Load(i.sv_vertexid + vertexCount).xyz * (1 - weight);
	float3 t1 = float3(dot(float4(t0, 1), transpose(bones[i.bones.x])[0]), dot(float4(t0, 1), transpose(bones[i.bones.x])[1]), dot(float4(t0, 1), transpose(bones[i.bones.x])[2])) * i.weights.x + float3(dot(float4(t0, 1), transpose(bones[i.bones.y])[0]), dot(float4(t0, 1), transpose(bones[i.bones.y])[1]), dot(float4(t0, 1), transpose(bones[i.bones.y])[2])) * i.weights.y;
	return mul(float4(t1, 1), vp);
}
