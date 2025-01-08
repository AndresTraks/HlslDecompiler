sampler sampler0;

struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
};

PS_OUT main(float2 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.color = tex2D(sampler0, texcoord);
	o.color1 = tex2Dgrad(sampler0, texcoord, texcoord, texcoord.yx);

	return o;
}
