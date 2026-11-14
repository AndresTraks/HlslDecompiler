float4 c;
float4 k;

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = color * c + k;
	o = r0.w * r0;

	return o;
}
