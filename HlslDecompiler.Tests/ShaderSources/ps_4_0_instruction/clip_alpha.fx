float cutoff;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = texcoord.w + -(cutoff.x);
	clip(r0.x);
	o = texcoord;

	return o;
}
