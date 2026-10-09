float4 scales[4] : register(c4);
float4 threshold : register(c8);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.color = step(threshold, i.color) * i.color * scales[trunc(i.texcoord.x)] + ((i.color < threshold) ? 1 : 0) * frac(i.color) + exp2(i.color.x) + log2(i.color.y);
	o.position = mul(i.position, wvp);

	return o;
}
