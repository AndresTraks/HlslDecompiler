sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = 5 + texcoord.yz;
	if (-texcoord.y < 0) {
		r1 = tex2Dlod(sampler0, texcoord);
		r0.zw = r0.xy + r1.xy;
	} else {
		r2 = texcoord + texcoord;
		r1 = tex2Dlod(sampler0, r2);
		r0.zw = r0.xy + -r1.xy;
	}
	if (texcoord.y >= 0) {
		r2 = 1 + texcoord;
		r2 = tex2Dlod(sampler0, r2);
		r0.zw = r0.zw + r2.xy;
	} else {
		r1.zw = float2(3, 4);
		r1.xy = float2(1, 0);
	}
	r0.xy = r0.zw + r1.xy;
	r0 = tex2D(sampler0, r0.xy);
	r0 = r0 + r1;
	r1 = r1 + float4(1, 0, 3, 4);
	o = (-texcoord.x >= 0) ? r0 : r1;

	return o;
}
