float4 c;
float4 k;

float4 main(float4 color : COLOR) : COLOR
{
	float4 o;

	float4 r0;
	r0 = c;
	r0 = color * r0 + k;
	o = r0.w * r0;

	return o;
}
