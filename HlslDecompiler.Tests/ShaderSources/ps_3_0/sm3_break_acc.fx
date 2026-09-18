float4 k;
sampler2D source;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float3 t0 = 0;
	float2 t1 = texcoord;
	for (int i = 0; i < 8; i++) {
		float3 t2 = tex2Dlod(source, float4(t1, 0, 0)).xyz * k.x + t0;
		if (k.y < t2.x) {
			t0 = t2;
			break;
		}
		t0 = t2;
		t1 = t1 + k.zw;
	}
	return float4(t0, 1);
}
