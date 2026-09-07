float4 c;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o = dot(texcoord.ww, texcoord.xx) + c.w;

	return o;
}
