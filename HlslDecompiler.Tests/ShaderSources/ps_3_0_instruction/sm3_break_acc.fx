float4 k;
sampler2D source;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float3 r0;
	float2 r1;
	float4 r2;
	r0 = 0;
	r1 = texcoord.xy;
	for (int i0 = 0; i0 < 8; i0++) {
		r2 = r1.xyxx * float4(1, 1, 0, 0);
		r2 = tex2Dlod(source, r2);
		r2.xyz = r2.xyz * k.xxx + r0.xyz;
		if (k.y < r2.x) {
			r0 = r2.xyz;
			if (1 != -1) break;
		}
		r1 = r1.xy + k.zw;
		r0 = r2.xyz;
	}
	o.xyz = r0.xyz;
	o.w = 1;

	return o;
}
