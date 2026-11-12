float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	half4 r1;
	float r2;
	r0 = half4(tex2D(s0, texcoord.xy));
	r1 = r0 * k;
	r2 = -r0.x + 0.5;
	r0 = r0 * k2;
	o = (r2.x >= 0) ? r0 : r1;

	return o;
}
