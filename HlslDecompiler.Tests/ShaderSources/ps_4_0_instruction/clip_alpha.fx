float cutoff;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = texcoord.w + -(cutoff);
	r0 = (r0.x < 0) ? -1 : 0;
	clip(r0.x);
	o = texcoord;

	return o;
}
