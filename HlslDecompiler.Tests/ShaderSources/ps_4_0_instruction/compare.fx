float k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = (texcoord.x < k.x) ? -1 : 0;
	o = (r0.x != 0) ? texcoord : -(texcoord);

	return o;
}
