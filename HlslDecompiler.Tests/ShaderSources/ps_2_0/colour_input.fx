float4 k;
sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	return lerp(tex2D(s0, i.texcoord), i.color, k.x) * k + saturate(tex2D(s0, i.texcoord) - i.color);
}
