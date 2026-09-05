float4 a;
float4 b;
float t;

float4 main(float texcoord : TEXCOORD) : COLOR
{
	return saturate(texcoord * t) * (b - a) + a;
}
