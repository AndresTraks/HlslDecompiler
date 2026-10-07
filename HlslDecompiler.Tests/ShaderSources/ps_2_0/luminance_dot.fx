float4 main(float4 color : COLOR) : COLOR
{
	float t0 = 0.300000012 * color.x + 0.589999974 * color.y + 0.109999999 * color.z;
	return t0 * color;
}
