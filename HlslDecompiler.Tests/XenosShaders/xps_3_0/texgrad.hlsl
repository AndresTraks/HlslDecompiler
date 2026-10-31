sampler2D s;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	return tex2Dgrad(s, i.texcoord, i.texcoord1, i.texcoord1.yx);
}
