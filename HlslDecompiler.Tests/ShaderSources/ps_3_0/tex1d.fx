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

	float4 t0 = tex1Dbias(sampler0, texcoord);
	float4 t1 = tex1Dlod(sampler0, texcoord);
	float4 t2 = tex1Dproj(sampler0, texcoord.xyyw);
	o.color = tex1D(sampler0, texcoord.x) + t0;
	o.color1 = tex1Dgrad(sampler0, texcoord.x, texcoord.x, texcoord.y);
	o.color2 = tex1Dgrad(sampler0, 1, texcoord.x, texcoord.x);
	o.color3 = t1 + t2;

	return o;
}
