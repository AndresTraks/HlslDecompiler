float blend;
sampler2D detail;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	half3 t0 = 4 * tex2D(detail, texcoord).xyz - 2;
	half3 t1 = lerp(t0, float3(1, 0, 0), blend);
	return float4(t1, 0.5);
}
