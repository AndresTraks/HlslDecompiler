float4 control : register(c8);
float4 offsets[8];
sampler2D source;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float2 r5;
	r0.xy = float2(0, 1);
	r1.zw = r0.xy * control.yy;
	r2 = 0;
	r0.yz = 0;
	for (int i0 = 0; i0 < 8; i0++) {
		r3 = r0.z + float4(-0, -1, -2, -3);
		r4 = r0.z + float4(-4, -5, -6, -7);
		r0.w = (-abs(r3.x) >= 0) ? offsets[0].w : r0.x;
		r0.w = (-abs(r3.y) >= 0) ? offsets[1].w : r0.w;
		r0.w = (-abs(r3.z) >= 0) ? offsets[2].w : r0.w;
		r0.w = (-abs(r3.w) >= 0) ? offsets[3].w : r0.w;
		r0.w = (-abs(r4.x) >= 0) ? offsets[4].w : r0.w;
		r0.w = (-abs(r4.y) >= 0) ? offsets[5].w : r0.w;
		r0.w = (-abs(r4.z) >= 0) ? offsets[6].w : r0.w;
		r0.w = (-abs(r4.w) >= 0) ? offsets[7].w : r0.w;
		if (-r0.w >= 0) {
			if (1 != -1) break;
		}
		r5 = (-abs(r3.xx) >= 0) ? offsets[0].xy : r0.xx;
		r3.xy = (-abs(r3.yy) >= 0) ? offsets[1].xy : r5.xy;
		r3.xy = (-abs(r3.zz) >= 0) ? offsets[2].xy : r3.xy;
		r3.xy = (-abs(r3.ww) >= 0) ? offsets[3].xy : r3.xy;
		r3.xy = (-abs(r4.xx) >= 0) ? offsets[4].xy : r3.xy;
		r3.xy = (-abs(r4.yy) >= 0) ? offsets[5].xy : r3.xy;
		r3.xy = (-abs(r4.zz) >= 0) ? offsets[6].xy : r3.xy;
		r3.xy = (-abs(r4.ww) >= 0) ? offsets[7].xy : r3.xy;
		r1.xy = r3.xy * control.xx + texcoord.xy;
		r3 = tex2Dlod(source, r1);
		r2 = r3 * r0.w + r2;
		r0.y = r0.w + r0.y;
		r0.z = r0.z + 1;
	}
	r0.x = r0.y + -0.0001;
	r0.y = 1 / r0.y;
	r0.x = (r0.x >= 0) ? r0.y : 10000;
	o = r0.x * r2;

	return o;
}
