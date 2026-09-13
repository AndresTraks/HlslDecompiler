float4 a;
float4 b;
float4 c;
float t;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float t0 = t - texcoord.x;
	float4 t1;
	if (t < texcoord.x) {
		if (t < texcoord.y) {
			t1 = a;
		} else {
			t1 = b;
		}
	} else {
		t1 = 0;
	}
	return t0 >= 0 ? c : t1;
}
