sampler3D sampler0;

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

	o.color = tex3D(sampler0, texcoord.xyz);
	o.color1 = tex3Dgrad(sampler0, texcoord.xyz, texcoord.xyz, texcoord.yxz);
	o.color2 = tex3Dgrad(sampler0, float3(1, 2, 3), texcoord.xyz, texcoord.xyz);
	o.color3 = tex3Dlod(sampler0, texcoord) + tex3Dproj(sampler0, texcoord.xyyw);

	return o;
}
