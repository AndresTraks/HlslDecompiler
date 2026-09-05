float k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = (k.x < texcoord.x) ? -1 : 0;
	if (r0.x != 0) {
		o = float4(0, 0, 0, 0);
		return o;
	}
	o = texcoord;

	return o;
}
