float4 a;
float4 b;
float4 c;
float t;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0;
	if (t < texcoord.x) {
		if (t < texcoord.y) {
			t0 = a;
		} else {
			t0 = b;
		}
	} else {
		t0 = 0;
	}
	return t - texcoord.x >= 0 ? c : t0;
}
