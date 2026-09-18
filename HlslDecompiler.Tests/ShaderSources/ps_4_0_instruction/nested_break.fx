float4 search;

SamplerState samp;
Texture2D field;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	float3 r2;
	float3 r3;
	float4 r4;
	r0 = int4(0, 0, 0, 0);
	while (true) {
		r1.x = (r0.w >= 4) ? -1 : 0;
		if (r1.x != 0) break;
		r1.z = asint((float)(int)r0.w);
		r2 = r0.xyz;
		r1.x = 0;
		while (true) {
			r1.w = (r1.x >= 4) ? -1 : 0;
			if (r1.w != 0) break;
			r1.y = asint((float)r1.x);
			r3.yz = asfloat(r1.yz) * search.zw + texcoord.xy;
			r4 = field.SampleLevel(samp, r3.yz, 0);
			r3.x = max(r2.x, r4.x);
			r1.y = (search.x < r4.x) ? -1 : 0;
			if (r1.y != 0) {
				r2 = r3.xyz;
				break;
			}
			r1.x = r1.x + 1;
			r2 = r3.xyz;
		}
		r1.x = (search.y < r2.x) ? -1 : 0;
		if (r1.x != 0) {
			r0.xyz = r2.xyz;
			break;
		}
		r0.w = r0.w + 1;
		r0.xyz = r2.xyz;
	}
	o.xyz = r0.yzx;
	o.w = 1;

	return o;
}
