float4 real[32];
float4 dual[32];
float4x4 vp;

struct VS_IN
{
	float3 position : POSITION;
	uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	float4 t0 = real[i.blendindices.x] * i.blendweight.x + i.blendweight.y * real[i.blendindices.y];
	float t1 = length(t0);
	float4 t2 = (dual[i.blendindices.x] * i.blendweight.x + i.blendweight.y * dual[i.blendindices.y]) / t1;
	float4 t3 = t0 / t1;
	float3 t4 = 2 * cross(t3.xyz, cross(t3.xyz, i.position) + t3.w * i.position) + i.position + 2 * (cross(t3.xyz, t2.xyz) + (t3.w * t2.xyz - t3.xyz * t2.w));
	return mul(float4(t4, 1), vp);
}
