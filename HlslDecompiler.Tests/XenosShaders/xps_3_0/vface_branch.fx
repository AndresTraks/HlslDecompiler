float4 k;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	return i.vface > 0 ? i.texcoord * k : i.texcoord.wzyx;
}
