float4 bones[16];
float4x4 wvp;

struct VS_IN
{
	float4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : POSITION
{
	return mul(bones[i.blendindices.z] * i.blendweight.z + bones[i.blendindices.x] * i.blendweight.x + i.blendweight.y * bones[i.blendindices.y], wvp);
}
