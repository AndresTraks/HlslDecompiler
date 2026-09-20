int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int r0;
	float4 r1;
	r0 = k.x * -1640531535 + 7;
	r0 = ((uint)r0.x < 1073741824) ? -1 : 0;
	r1 = texcoord + texcoord;
	o = (r0.x != 0) ? texcoord : r1;

	return o;
}
