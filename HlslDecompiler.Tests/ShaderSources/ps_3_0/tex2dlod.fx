sampler2D sampler0;
sampler3D sampler1;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = tex3Dlod(sampler1, texcoord);
	float4 t1 = tex2Dlod(sampler0, texcoord);
	return t1 + t0;
}
