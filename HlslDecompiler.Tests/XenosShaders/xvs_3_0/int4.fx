struct VS_IN
{
	float4 position : POSITION;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	return i.position * i.blendindices;
}
