int n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int r0;
	r0 = 3 * n + -7;
	r0 = asint((float)r0.x);
	o = asfloat(r0.x) * texcoord;

	return o;
}
