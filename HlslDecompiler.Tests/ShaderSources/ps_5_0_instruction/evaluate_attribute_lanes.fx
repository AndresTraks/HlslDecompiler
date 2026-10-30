struct PS_IN
{
	float4 sv_position : SV_Position;
	noperspective float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float3 r0;
	o.zw = float2(0, 1);
	r0 = EvaluateAttributeSnapped(i.normal.xyz, int2(3, -2));
	o.x = r0.y + 1;
	o.y = r0.x + r0.x;

	return o;
}
