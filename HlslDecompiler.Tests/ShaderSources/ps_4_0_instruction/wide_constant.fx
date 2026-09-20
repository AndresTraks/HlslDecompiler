int4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int r0;
	r0 = k.x * -1640531535 + -1640531527;
	r0 = r0.x & 65535;
	r0 = asint((float)(uint)r0.x);
	o = asfloat(r0.x) * texcoord;

	return o;
}
