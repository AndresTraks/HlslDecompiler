sampler2D sampler0;
sampler2D sampler1;
sampler2D sampler2;
sampler2D sampler5 : register(s5);

struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.color = tex2D(sampler0, texcoord.xy);
	o.color1 = tex2Dgrad(sampler2, texcoord.xy, texcoord.xy, texcoord.yx);
	o.color2 = tex2Dlod(sampler1, texcoord);
	o.color3 = tex2Dproj(sampler5, texcoord.xyyw);

	return o;
}
