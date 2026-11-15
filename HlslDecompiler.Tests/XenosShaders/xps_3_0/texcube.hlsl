samplerCUBE s;
sampler3D v;
sampler1D o;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	return texCUBE(s, texcoord) + tex3D(v, texcoord) + tex1D(o, texcoord.x);
}
