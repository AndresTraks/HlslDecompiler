float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = rcp(float2(k.y - k.x, k.w));
	float4 t1 = 0;
	float t2 = 0;
	for (int i = 0; i < 3; i++) {
		float4 t3 = saturate(t0.x * (texcoord.yzxw * t2 - k.x));
		float4 t4 = t3 * t3 * (-2 * t3 + 3) * ((texcoord.yzxw - t2 >= 0 ? 0 : -1) + (-(texcoord.yzxw - t2) >= 0 ? 0 : 1)) + t1.yzxw;
		float t5 = t0.y * t4.z;
		float t6 = frac(abs(t5));
		float t7 = (t5 >= 0 ? t6 : -t6) * -k.w + k.z;
		t1 = t7 >= 0 ? t4.zxyw : clamp(t4.zxyw, -k, k);
		t2 = t2 + 1;
	}
	return t1;
}
