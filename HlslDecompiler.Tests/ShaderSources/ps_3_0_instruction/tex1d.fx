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

	float4 r0;
	float4 r1;
	r0 = tex1D(sampler0, texcoord.x);
	r1 = tex1Dbias(sampler0, texcoord);
	o.color = r0 + r1;
	o.color1 = tex1Dgrad(sampler0, texcoord.x, texcoord.x, texcoord.y);
	o.color2 = tex1Dgrad(sampler0, 1, texcoord.x, texcoord.x);
	r0 = tex1Dlod(sampler0, texcoord);
	r1 = tex1Dproj(sampler0, texcoord.xyyw);
	o.color3 = r0 + r1;

	return o;
}
