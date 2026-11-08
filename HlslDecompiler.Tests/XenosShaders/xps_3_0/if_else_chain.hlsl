float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.x;
	float3 t1 = texcoord.yzw;
	float t2 = 1 - ((k.x < t0) ? 1 : 0);
	if (k.x < t0) {
		float t3 = 2 * t0;
		t0 = 2 * t1.z;
		t1 = float3(t3, 2 * t1.xy);
	}
	float t4 = t2 - 1;
	float t5 = t2 == 0 ? 1 : t2;
	float t6 = t4 == 0 ? 1.0 : 0.0;
	if (t6 != 0) {
		t6 = ((k.y < t1.x) ? 1 : 0) == 0 ? 1.0 : 0.0;
	}
	if (t6 != 0) {
		float t7 = k.x - t0;
		t0 = k.w - t1.z;
		t1 = float3(t7, k.yz - t1.xy);
	}
	return float4(t1, t0);
}
