sampler2D s0;
float4 steps[8];

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float4 r3;
	float4 r4;
	float3 r5;
	r0.zw = 0;
	r1 = 0;
	r2.x = 0;
	for (int i0 = 0; i0 < 8; i0++) {
		r3 = r2.x + float4(-0, -1, -2, -3);
		r4 = r2.x + float4(-4, -5, -6, -7);
		r5.x = 0;
		r2.y = (-abs(r3.x) >= 0) ? steps[0].z : r5.x;
		r2.y = (-abs(r3.y) >= 0) ? steps[1].z : r2.y;
		r2.y = (-abs(r3.z) >= 0) ? steps[2].z : r2.y;
		r2.y = (-abs(r3.w) >= 0) ? steps[3].z : r2.y;
		r2.y = (-abs(r4.x) >= 0) ? steps[4].z : r2.y;
		r2.y = (-abs(r4.y) >= 0) ? steps[5].z : r2.y;
		r2.y = (-abs(r4.z) >= 0) ? steps[6].z : r2.y;
		r2.y = (-abs(r4.w) >= 0) ? steps[7].z : r2.y;
		r2.z = r2.x + 1;
		if (r2.y >= 0) {
			r2.y = (r2.y >= 0) ? r2.x : r2.z;
			r3 = r2.y + float4(-0, -1, -2, -3);
			r4 = r2.y + float4(-4, -5, -6, -7);
			r5 = (-abs(r3.xxx) >= 0) ? steps[0].xyz : r5.xxx;
			r5 = (-abs(r3.yyy) >= 0) ? steps[1].xyz : r5.xyz;
			r3.xyz = (-abs(r3.zzz) >= 0) ? steps[2].xyz : r5.xyz;
			r3.xyz = (-abs(r3.www) >= 0) ? steps[3].xyz : r3.xyz;
			r3.xyz = (-abs(r4.xxx) >= 0) ? steps[4].xyz : r3.xyz;
			r3.xyz = (-abs(r4.yyy) >= 0) ? steps[5].xyz : r3.xyz;
			r3.xyz = (-abs(r4.zzz) >= 0) ? steps[6].xyz : r3.xyz;
			r3.xyz = (-abs(r4.www) >= 0) ? steps[7].xyz : r3.xyz;
			r0.xy = r3.xy + texcoord.xy;
			r4 = tex2Dlod(s0, r0);
			r1 = r4 * r3.z + r1;
		}
		r2.x = r2.z;
	}
	o = r1;

	return o;
}
