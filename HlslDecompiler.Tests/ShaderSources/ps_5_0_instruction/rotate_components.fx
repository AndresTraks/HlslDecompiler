float4 v;
int n;

float4 main() : SV_Target
{
	float4 o;

	float4 r0;
	int r1;
	r0.xyz = v.xyz;
	r0.w = 0;
	while (true) {
		r1 = (r0.w >= n) ? -1 : 0;
		if (r1.x != 0) break;
		r0.xyz = r0.yzx * float3(1.5, 1.5, 1.5);
		r0.w = r0.w + 1;
	}
	o.xyz = r0.xyz;
	o.w = v.w;

	return o;
}
