sampler1D sampler0;

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

	o.color = tex1D(sampler0, texcoord.x) + tex1Dbias(sampler0, texcoord);
	o.color1 = tex1Dgrad(sampler0, texcoord.x, texcoord.x, texcoord.y);
	o.color2 = tex1Dgrad(sampler0, 1, texcoord.x, texcoord.x);
	o.color3 = tex1Dlod(sampler0, texcoord) + tex1Dproj(sampler0, texcoord.xyyw);

	return o;
}
