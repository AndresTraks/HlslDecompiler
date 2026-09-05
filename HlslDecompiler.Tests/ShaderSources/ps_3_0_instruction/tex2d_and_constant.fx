float scale;
sampler2D tex;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = tex2D(tex, texcoord.xy);
	o = r0 * scale.x;

	return o;
}
