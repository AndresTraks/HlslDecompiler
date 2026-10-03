sampler2D sampler0;
sampler2D sampler1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = tex2D(sampler1, texcoord.yx).xy;
	return tex2D(sampler0, 2 * t0 + texcoord.yx).wzyx;
}
