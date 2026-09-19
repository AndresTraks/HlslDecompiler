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

	o.position = mul(i.position, wvp);
	o.color = ((i.color >= threshold) ? 1 : 0) * i.color * scales[((i.texcoord.x < -i.texcoord.x) ? 1 : 0) * ((-frac(i.texcoord.x) < frac(i.texcoord.x)) ? 1 : 0) + floor(i.texcoord.x)] + ((i.color < threshold) ? 1 : 0) * frac(i.color) + exp2(i.color.x) + log2(i.color.y);

	return o;
}
