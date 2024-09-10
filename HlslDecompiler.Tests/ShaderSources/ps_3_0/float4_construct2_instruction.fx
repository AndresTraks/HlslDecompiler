struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.color = i.texcoord.xxxy * float4(1, 0, 0, 1) + float4(0, 0, 1, 0);
	o.color1 = i.texcoord.xxxy * float4(0, 0, 1, 1) + float4(0, 1, 0, 0);
	o.color2 = i.texcoord.x * float4(0, 0, 0, 1) + float4(0, 1, 2, 0);
	o.color3.xy = i.texcoord.xy;
	o.color3.zw = i.texcoord1.zw;

	return o;
}
