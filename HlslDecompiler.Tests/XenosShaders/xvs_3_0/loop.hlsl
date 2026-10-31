float4 k[8];
int n : register(i0);

float4 main(float4 position : POSITION) : POSITION
{
	float2 t0 = position.x;
	float3 t1 = position.yzw;
	for (int i = 0; i < n; i++) {
		t0 = t0.x + position.x * k[i].x;
		t1 = t1 + position.x * k[i].yzw;
	}
	return float4(t0.y, t1);
}
