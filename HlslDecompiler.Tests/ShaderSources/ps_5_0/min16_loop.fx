min16float4 c;
int n;

float4 main(min16float4 color : COLOR) : SV_Target
{
	float4 t0 = color;
	for (int t1 = 0; t1 < n; t1 = t1 + 1) {
		t0 = t0 * c + color;
	}
	return t0;
}
