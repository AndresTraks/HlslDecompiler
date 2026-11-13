float4 params;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float r2;
	float4 r3;
	r0.zw = 0;
	r1 = 0;
	r2 = 0;
	for (int i0 = 0; i0 < 12; i0++) {
		if (r2.x >= params.x) break;
		r0.xy = r2.xx * params.zw + texcoord.xy;
		r3 = tex2Dlod(s0, r0);
		r1 = r1 + r3;
		r2 = r2.x + params.y;
	}
	o = r1;

	return o;
}
