sampler2D s0;
float4 tint;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(s0, texcoord.xy);
	r0 = r0 * tint;
	o = r0;

	return o;
}
