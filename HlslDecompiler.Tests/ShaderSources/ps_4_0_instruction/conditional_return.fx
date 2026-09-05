float4 a;
float4 b;
float4 c;
float t;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = (t < texcoord.x) ? -1 : 0;
	o = a;
	if (r0.x != 0) return o;
	r0 = (t < texcoord.y) ? -1 : 0;
	if (r0.x != 0) {
		o = b;
		return o;
	}
	o = c;

	return o;
}
