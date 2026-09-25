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
	float4 o;

	float3 r0;
	r0 = EvaluateAttributeCentroid(i.normal.xyz);
	o.z = dot(r0.xyz, weight);
	o.xy = EvaluateAttributeSnapped(i.texcoord.xy, snapOffset.xy);
	o.w = 1;

	return o;
}
