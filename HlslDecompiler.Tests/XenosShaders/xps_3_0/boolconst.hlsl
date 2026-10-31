bool b : register(b0);
float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = texcoord;
	if (b) {
		t0 = t0 * k;
	}
	return t0;
}
