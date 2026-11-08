float4 k;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	return lerp(i.texcoord, i.texcoord1, k.x) + saturate(i.texcoord * i.texcoord1) + step(i.texcoord, i.texcoord1) + max(i.texcoord, i.texcoord1) + min(i.texcoord, i.texcoord1);
}
