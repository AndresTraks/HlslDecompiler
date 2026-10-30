struct PS_IN
{
	float4 sv_position : SV_Position;
	noperspective float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(EvaluateAttributeSnapped(i.normal, int2(3, -2)).y + 1, 2 * EvaluateAttributeSnapped(i.normal, int2(3, -2)).x, 0, 1);
}
