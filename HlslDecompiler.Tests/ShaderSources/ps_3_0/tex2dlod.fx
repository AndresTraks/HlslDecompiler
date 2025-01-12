sampler2D sampler0;
sampler3D sampler1;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return tex2Dlod(sampler0, texcoord) + tex3Dlod(sampler1, texcoord);
}
