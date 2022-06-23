struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 color : SV_Target;
	float4 color1 : SV_Target1;
	float4 color2 : SV_Target2;
	float4 color3 : SV_Target3;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.color = float4(i.texcoord.x, 0, 1, i.texcoord.y);
	o.color1 = float4(0, 1, i.texcoord);
	o.color2 = float4(0, 1, 2, i.texcoord.x);
	o.color3 = float4(i.texcoord, i.texcoord1.zw);

	return o;
}
