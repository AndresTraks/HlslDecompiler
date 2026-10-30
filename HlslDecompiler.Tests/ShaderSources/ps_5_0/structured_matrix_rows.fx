cbuffer c : register(b0)
{
	uint idx;
};

StructuredBuffer<float4x4> mats : register(t0);

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = transpose(mats[idx])[0];
	float4 t1 = transpose(mats[idx])[1];
	float4 t2 = transpose(mats[idx])[2];
	float t3 = dot(cross(t1.xyz, t2.xyz), t0.xyz);
	return float4(dot(t0, texcoord), dot(t1, texcoord), dot(t2, texcoord), dot(transpose(mats[idx])[3], texcoord)) + t3;
}
