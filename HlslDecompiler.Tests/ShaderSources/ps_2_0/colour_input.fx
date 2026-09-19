float4 k;
sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 t0 = tex2D(s0, i.texcoord);
	return lerp(t0, i.color, k.x) * k + saturate(t0 - i.color);
}
