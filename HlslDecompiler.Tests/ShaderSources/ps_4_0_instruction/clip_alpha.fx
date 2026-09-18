float cutoff;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int r0;
	r0 = asint(texcoord.w + -(cutoff));
	r0 = (asfloat(r0.x) < 0) ? -1 : 0;
	if (r0.x != 0) discard;
	o = texcoord;

	return o;
}
