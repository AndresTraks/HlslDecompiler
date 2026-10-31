struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
	float4 texcoord4 : TEXCOORD4;
	float4 texcoord5 : TEXCOORD5;
	float4 texcoord6 : TEXCOORD6;
	float4 texcoord7 : TEXCOORD7;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord = i.color;
	o.texcoord1 = 2 * i.color;
	o.texcoord2 = i.color.wzyx;
	o.texcoord3 = i.position * i.color;
	o.texcoord4 = i.position + i.color;
	o.texcoord5 = i.position.yzwx;
	o.texcoord6 = float4(i.position.x * i.color.x, i.color.yzw * i.position.xxx);
	o.texcoord7 = frac(i.position);
	o.position = i.position;

	return o;
}
