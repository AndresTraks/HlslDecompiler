float4 pal[16] : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position = mul(i.position, wvp);
	o.color = pal[clamp(trunc(8 * i.texcoord.x - 4), 0, 15)];

	return o;
}
