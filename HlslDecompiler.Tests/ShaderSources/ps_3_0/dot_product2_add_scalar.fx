float4 c;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = dot(texcoord.ww, texcoord.xx);
	return t0 + c.w;
}
