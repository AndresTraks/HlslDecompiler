float4 c;

float4 main(float4 color : COLOR) : SV_Target
{
	float4 t0 = saturate(color * c);
	return float4(normalize(t0.xyz), dot(c, t0));
}
