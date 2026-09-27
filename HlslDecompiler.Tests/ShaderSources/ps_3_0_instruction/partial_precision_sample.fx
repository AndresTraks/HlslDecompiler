float4 k;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	half4 r1;
	r0 = tex2D(tex, texcoord.xy);
	r1 = r0 * k.x;
	o = r0 * r1;

	return o;
}
