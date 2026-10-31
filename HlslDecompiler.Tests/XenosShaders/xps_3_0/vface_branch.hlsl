float4 k;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float2 vpos : VPOS;
	float vface : VFACE;
};

float4 main(PS_IN i) : COLOR
{
	float t0 = 2 * ((1 / (i.vface >= 0 ? i.vpos.x : -i.vpos.x)) >= 0 ? 1 : 0) - 1;
	return -t0 >= 0 ? i.texcoord.wzyx : i.texcoord * k;
}
