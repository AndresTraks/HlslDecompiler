float4 cutoff;
sampler2D diffuse;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	clip(tex2D(diffuse, texcoord).w - cutoff.x);
	return tex2D(diffuse, texcoord) * cutoff.y;
}
