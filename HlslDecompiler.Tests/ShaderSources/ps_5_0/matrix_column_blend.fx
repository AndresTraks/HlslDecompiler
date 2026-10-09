float3x4 bones[4];

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(PS_IN i) : SV_Target
{
	return float4((transpose(bones[i.blendindices.x])[0].xyz * i.texcoord.x + i.texcoord.y * transpose(bones[i.blendindices.x])[1].xyz + transpose(bones[i.blendindices.x])[2].xyz * i.texcoord.z + transpose(bones[i.blendindices.x])[3].xyz * i.texcoord.w) * i.blendweight.x + (transpose(bones[i.blendindices.y])[0].xyz * i.texcoord.x + i.texcoord.y * transpose(bones[i.blendindices.y])[1].xyz + transpose(bones[i.blendindices.y])[2].xyz * i.texcoord.z + transpose(bones[i.blendindices.y])[3].xyz * i.texcoord.w) * i.blendweight.y, 1);
}
