float4 c;
int n;

float4 main() : COLOR
{
	float4 o;

	float3 r0;
	r0 = c.xyw;
	for (int i0 = 0; i0 < n; i0++) {
		r0.y = r0.y * 2 + 1;
		r0.x = r0.y + c.z;
		r0.z = r0.x + -c.w;
	}
	o.xyw = r0.xyz;
	o.z = c.z;

	return o;
}
