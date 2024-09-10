sampler sampler0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	o = tex2D(sampler0, texcoord);

	return o;
}
