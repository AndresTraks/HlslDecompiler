float4x4 m[4];
int n : register(i0);

float4 main(float4 position : POSITION) : POSITION
{
	float4 t0 = float4(position.x, abs(position.x) >= 0 ? 0 : 1, position.yz);
	float t1 = position.w;
	for (int i = 0; i < n; i++) {
		float t2 = dot(float4(t1, t0.wxz), transpose(m[t0.y])[1].wzxy);
		float t3 = dot(float4(t1, t0.wxz), transpose(m[t0.y])[2].wzxy);
		float t4 = dot(float4(t1, t0.wxz), transpose(m[t0.y])[3].wzxy);
		t0.x = dot(float4(t1, t0.wxz), transpose(m[t0.y])[0].wzxy);
		t0.y = t0.y + 1;
		t0.z = t2;
		t0.w = t3;
		t1 = t4;
	}
	return float4(t0.xzw, t1);
}
