float4 tint : register(c4);
float4x4 wvp;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord = i.texcoord.xy;
	o.color = 2 * tint;
	o.position = mul(i.position, wvp);

	return o;
}
