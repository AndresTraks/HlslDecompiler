float4 k;
float4 k3;
sampler2D s0;
sampler2D s1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	float4 r1;
	r0 = tex2D(s0, texcoord.xy);
	r1.x = k.x;
	if (-r1.x < 0) {
		r1 = tex2D(s1, texcoord.xy);
		r1 = half4(r0 * r1);
	} else {
		r1 = half4(r0 * k3);
	}
	o = r1 * k;

	return o;
}
