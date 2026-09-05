int n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = 3 * n.x + -7;
	r0 = r0.x;
	o = r0.x * texcoord;

	return o;
}
