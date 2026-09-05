float4 a;
float4 b;
float4 c;
float t;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float r0;
	float4 r1;
	r0 = t.x + -texcoord.x;
	if (t.x < texcoord.x) {
		if (t.x < texcoord.y) {
			r1 = a;
		} else {
			r1 = b;
		}
	} else {
		r1 = 0;
	}
	o = (r0.x >= 0) ? c : r1;

	return o;
}
