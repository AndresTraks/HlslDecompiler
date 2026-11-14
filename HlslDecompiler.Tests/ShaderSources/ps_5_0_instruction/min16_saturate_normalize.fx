min16float4 c;

float4 main(min16float4 color : COLOR) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = saturate(color * c);
	r1.x = dot(r0.xyz, r0.xyz);
	r1.x = rsqrt(r1.x);
	r1.xyz = r0.xyz * r1.xxx;
	r1.w = dot(r0, c);
	o = r1;

	return o;
}
