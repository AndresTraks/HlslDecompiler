cbuffer C : register(b0)
{
	float4 lim;
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = texcoord;
	for (int t1 = 0; t1 < 6; t1 = t1 + 1) {
		t0 = 1.5 * t0 - lim;
		if (lim.y < t0.x) {
			if (t0.y < 0) return float4(t0.zw, (float)t1, 1);
			t0.z = t0.z + 1;
		}
	}
	return t0;
}
