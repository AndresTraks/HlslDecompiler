samplerCUBE c : register(s1);
sampler2D s;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return tex2Dbias(s, float4(texcoord.xy, 0, texcoord.w)) + texCUBElod(c, float4(texcoord.xyz, texcoord.w));
}
