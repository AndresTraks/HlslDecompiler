float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float r0;
	float4 r1;
	r0 = threshold.x + -texcoord.x;
	r1 = texcoord + texcoord;
	o = (r0.x >= 0) ? texcoord : r1;

	return o;
}
