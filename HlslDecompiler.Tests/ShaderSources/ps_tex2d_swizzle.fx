sampler sampler0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return tex2D(sampler0, texcoord).wzyx;
}
