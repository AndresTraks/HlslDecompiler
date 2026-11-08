float4x3 bones[20];
float4x4 vp;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	float3 t0 = mul(i.position, bones[trunc(i.blendindices.w)]) * i.blendweight.w + mul(i.position, bones[trunc(i.blendindices.z)]) * i.blendweight.z + mul(i.position, bones[trunc(i.blendindices.y)]) * i.blendweight.y + mul(i.position, bones[trunc(i.blendindices.x)]) * i.blendweight.x;
	return mul(float4(t0, abs(trunc(i.blendindices.w)) >= 0), vp);
}
