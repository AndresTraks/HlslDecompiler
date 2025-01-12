sampler2D sampler0;
sampler2D sampler1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	return tex2D(sampler0, 2 * tex2D(sampler1, texcoord.yx).xy + texcoord.yx).wzyx;
}
