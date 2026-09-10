float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float r0;
	r0 = texcoord.y + texcoord.x;
	r0 = r0.x * r0.x + r0.x;
	r0 = r0.x * r0.x + r0.x;
	r0 = r0.x * r0.x + r0.x;
	r0 = r0.x * r0.x + r0.x;
	r0 = r0.x * r0.x + r0.x;
	o = r0.x * r0.x + r0.x;

	return o;
}
