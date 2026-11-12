float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	float4 r1;
	r0 = tex2D(s0, texcoord.xy);
	r1 = r0.w + -k.x;
	o = r0 * k2;
	clip(r1);

	return o;
}
