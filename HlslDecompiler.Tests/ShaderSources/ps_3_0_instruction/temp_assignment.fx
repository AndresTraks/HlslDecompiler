sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	if (-texcoord.y < 0) {
		r0 = tex2Dlod(sampler0, texcoord);
		r1.xy = r0.xy;
	} else {
		r1.xy = 0;
		r0 = float4(1, 0, 3, 4);
	}
	r1 = tex2D(sampler0, r1.xy);
	r1 = r0 + r1;
	r0 = r0 + float4(1, 0, 3, 4);
	o = (-texcoord.x >= 0) ? r1 : r0;

	return o;
}
