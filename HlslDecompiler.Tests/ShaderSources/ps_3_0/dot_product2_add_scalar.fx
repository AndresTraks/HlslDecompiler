float4 c;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	return float4(dot(texcoord.ww, texcoord.xx) + c.w, dot(texcoord.ww, texcoord.xx) + c.w, dot(texcoord.ww, texcoord.xx) + c.w, dot(texcoord.ww, texcoord.xx) + c.w);
}
