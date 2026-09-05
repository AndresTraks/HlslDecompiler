float4 a;
float4 b;
float4 c;
float4 d;
float t;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = (t.x < texcoord.x) ? -1 : 0;
	if (r0.x != 0) {
		r0 = (t.x < texcoord.y) ? -1 : 0;
		if (r0.x != 0) {
			r0 = (t.x < texcoord.z) ? -1 : 0;
			if (r0.x != 0) {
				o = a;
				return o;
			} else {
				o = b;
				return o;
			}
		}
		o = c;
		return o;
	}
	o = d;

	return o;
}
