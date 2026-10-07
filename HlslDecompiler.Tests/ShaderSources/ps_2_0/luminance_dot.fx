float4 main(float4 color : COLOR) : COLOR
{
	float t0 = dot(float3(0.300000012, 0.589999974, 0.109999999), color.xyz);
	return t0 * color;
}
