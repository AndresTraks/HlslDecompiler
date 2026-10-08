float4 bones[16];
float4x4 wvp;

struct VS_IN
{
	float4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : POSITION
{
	float3 t0 = floor(i.blendindices.xyz);
	return mul(bones[t0.x] * i.blendweight.x + i.blendweight.y * bones[t0.y] + bones[t0.z] * i.blendweight.z, wvp);
}
