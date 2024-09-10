sampler sampler0;
sampler sampler1;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = tex2Dlod(sampler0, texcoord);
	r1 = tex2Dlod(sampler1, texcoord);
	o = r0 + r1;

	return o;
}
