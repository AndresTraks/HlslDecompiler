float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	half4 r1;
	r0 = tex2D(tex, texcoord.xy);
	r1 = r0 * k;
	r0 = r0 * r1;
	o = r0.wwwy + r0.xyzx;

	return o;
}
