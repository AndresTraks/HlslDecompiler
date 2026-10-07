float4 main(float4 color : COLOR) : COLOR
{
	float4 o;

	float4 r0;
	r0.w = dot(color.xyz, float3(0.3, 0.59, 0.11));
	r0 = r0.w * color;
	o = r0;

	return o;
}
