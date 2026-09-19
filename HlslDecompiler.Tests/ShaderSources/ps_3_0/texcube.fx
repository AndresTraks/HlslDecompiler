samplerCUBE sampler0;

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

	float4 t0 = texCUBEproj(sampler0, texcoord.xyyw);
	float4 t1 = texCUBElod(sampler0, texcoord);
	float4 t2 = texCUBEbias(sampler0, texcoord);
	float4 t3 = texCUBE(sampler0, texcoord.xyz);
	o.color = t3 + t2;
	o.color1 = texCUBEgrad(sampler0, texcoord.xyz, texcoord.xyz, texcoord.yxz);
	o.color2 = texCUBEgrad(sampler0, float3(1, 2, 3), texcoord.xyz, texcoord.xyz);
	o.color3 = t1 + t0;

	return o;
}
