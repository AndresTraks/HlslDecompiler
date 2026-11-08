sampler2D s;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return tex2Dlod(s, texcoord) + tex2Dbias(s, texcoord) + tex2Dproj(s, texcoord);
}
