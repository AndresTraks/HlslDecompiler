sampler sampler0;
sampler sampler1;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return tex2Dlod(sampler0, texcoord) + tex3Dlod(sampler1, texcoord);
}
