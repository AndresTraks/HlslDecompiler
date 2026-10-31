samplerCUBE c : register(s1);
sampler2D s;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return tex2Dbias(s, texcoord.xyyw) + texCUBElod(c, texcoord);
}
