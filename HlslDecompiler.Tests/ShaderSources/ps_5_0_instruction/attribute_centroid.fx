struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	o.xy = EvaluateAttributeCentroid(i.texcoord.xy);
	o.zw = EvaluateAttributeCentroid(i.normal.xy);

	return o;
}
