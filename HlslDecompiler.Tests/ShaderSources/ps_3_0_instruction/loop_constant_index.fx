sampler2D s0;
float4 steps[16];
float threshold;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float4 r6;
	float r7;
	r0.zw = 0;
	r1 = 0;
	r2.x = 0;
	for (int i0 = 0; i0 < 16; i0++) {
		r3 = r2.x + float4(-0, -1, -2, -3);
		r4 = r2.x + float4(-4, -5, -6, -7);
		r5 = r2.x + float4(-8, -9, -10, -11);
		r6 = r2.x + float4(-12, -13, -14, -15);
		r7 = 0;
		r2.yz = (-abs(r3.xx) >= 0) ? steps[0].xy : r7.xx;
		r2.yz = (-abs(r3.yy) >= 0) ? steps[1].xy : r2.yz;
		r2.yz = (-abs(r3.zz) >= 0) ? steps[2].xy : r2.yz;
		r2.yz = (-abs(r3.ww) >= 0) ? steps[3].xy : r2.yz;
		r2.yz = (-abs(r4.xx) >= 0) ? steps[4].xy : r2.yz;
		r2.yz = (-abs(r4.yy) >= 0) ? steps[5].xy : r2.yz;
		r2.yz = (-abs(r4.zz) >= 0) ? steps[6].xy : r2.yz;
		r2.yz = (-abs(r4.ww) >= 0) ? steps[7].xy : r2.yz;
		r2.yz = (-abs(r5.xx) >= 0) ? steps[8].xy : r2.yz;
		r2.yz = (-abs(r5.yy) >= 0) ? steps[9].xy : r2.yz;
		r2.yz = (-abs(r5.zz) >= 0) ? steps[10].xy : r2.yz;
		r2.yz = (-abs(r5.ww) >= 0) ? steps[11].xy : r2.yz;
		r2.yz = (-abs(r6.xx) >= 0) ? steps[12].xy : r2.yz;
		r2.yz = (-abs(r6.yy) >= 0) ? steps[13].xy : r2.yz;
		r2.yz = (-abs(r6.zz) >= 0) ? steps[14].xy : r2.yz;
		r2.yz = (-abs(r6.ww) >= 0) ? steps[15].xy : r2.yz;
		r0.xy = r2.yz + texcoord.xy;
		r3 = tex2Dlod(s0, r0);
		if (threshold.x < r3.w) {
			break;
		}
		r1 = r1 + r3;
		r2.x = r2.x + 1;
	}
	o = r1;

	return o;
}
