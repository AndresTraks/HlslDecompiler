float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	r0 = -1;
	clip(r0);
	o = texcoord;

	return o;
}
