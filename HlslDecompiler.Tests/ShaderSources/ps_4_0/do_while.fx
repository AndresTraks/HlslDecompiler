float4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	int t0 = (int)k.y;
	float4 t1 = texcoord * k.x;
	for (int t2 = 1; t2 < t0; t2 = t2 + 1) {
		t1 = texcoord * k.x + t1;
	}
	return t1;
}
