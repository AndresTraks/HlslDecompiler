float4 a;
int n;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float t0 = 0;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = t0 + texcoord.x;
	}
	clip(t0);
	return a;
}
