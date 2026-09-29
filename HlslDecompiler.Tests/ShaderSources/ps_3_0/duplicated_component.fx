sampler2D sampler0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return float4(frac(texcoord.x), frac(texcoord.x), tex2D(sampler0, texcoord).ww);
}
