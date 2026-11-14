float4 c;

float4 main(float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	float r1;
	r0 = saturate(color * c);
	r1 = dot(r0.xyz, r0.xyz);
	r1 = rsqrt(r1.x);
	o.xyz = r0.xyz * r1.xxx;
	o.w = dot(r0, c);

	return o;
}
