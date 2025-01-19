sampler2D sampler0;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	if (-texcoord.y < 0) {
		r0 = tex2Dlod(sampler0, texcoord);
	} else {
		r0 = float4(1, 0, 3, 4);
	}
	if (-texcoord.x >= 0) {
		r1 = tex2D(sampler0, texcoord.xy);
		o = r0 + r1;
	} else {
		o = r0 + float4(1, 0, 3, 4);
	}

	return o;
}
