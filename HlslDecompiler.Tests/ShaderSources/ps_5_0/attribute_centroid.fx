struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(EvaluateAttributeCentroid(i.texcoord), EvaluateAttributeCentroid(i.normal.xy));
}
