cbuffer Params : register(b0)
{
	int2 snapOffset;
	float weight;
};

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(EvaluateAttributeSnapped(i.texcoord, snapOffset), dot(weight.xxx, EvaluateAttributeCentroid(i.normal)), 1);
}
