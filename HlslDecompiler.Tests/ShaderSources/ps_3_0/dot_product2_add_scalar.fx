float4 c;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return dot(texcoord.ww, texcoord.xx) + c.w;
}
