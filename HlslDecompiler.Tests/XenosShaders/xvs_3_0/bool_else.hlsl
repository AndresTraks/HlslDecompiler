bool b : register(b0);
float4 k;

float4 main(float4 position : POSITION) : POSITION
{
	float4 t0;
	if (b) {
		t0 = position * k;
	} else {
		t0 = position + k;
	}
	return t0;
}
