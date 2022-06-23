struct PS_OUT
{
	float4 color : SV_Target;
	float4 color1 : SV_Target1;
	float4 color2 : SV_Target2;
	float4 color3 : SV_Target3;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.color = texcoord;
	o.color1 = float4(texcoord.xyz, 0);
	o.color2 = float4(texcoord.xy, 0, 1);
	o.color3 = float4(texcoord.x, 0, 1, 2);

	return o;
}
