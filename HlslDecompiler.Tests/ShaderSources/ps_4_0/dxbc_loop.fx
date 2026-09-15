float count;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (float t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = t0 + texcoord;
	}
	return t0;
}
