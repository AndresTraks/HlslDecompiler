float4 c;
float4 k;

float4 main(float4 color : COLOR) : COLOR
{
	float4 t0 = color * c + k;
	return t0 * t0.w;
}
