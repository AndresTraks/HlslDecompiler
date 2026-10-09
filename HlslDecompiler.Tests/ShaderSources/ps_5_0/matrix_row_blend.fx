row_major float3x4 bones[4];

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(mul(bones[i.blendindices.x], i.texcoord) * i.blendweight.x + mul(bones[i.blendindices.y], i.texcoord) * i.blendweight.y, 1);
}
