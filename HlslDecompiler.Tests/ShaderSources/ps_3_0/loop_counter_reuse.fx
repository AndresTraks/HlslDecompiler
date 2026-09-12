float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 3; i++) {
		float4 t2 = saturate(rcp(k.y - k.x) * (texcoord.yzxw * t1 - k.x));
		float4 t3 = t2 * t2 * (-2 * t2 + 3) * ((texcoord.yzxw - t1 >= 0 ? 0 : -1) + (-(texcoord.yzxw - t1) >= 0 ? 0 : 1)) + t0.yzxw;
		float t4 = rcp(k.w) * t3.z;
		float t5 = frac(abs(t4));
		float t6 = (t4 >= 0 ? t5 : -t5) * -k.w + k.z;
		t0 = t6 >= 0 ? t3.zxyw : clamp(t3.zxyw, -k, k);
		t1 = t1 + 1;
	}
	return t0;
}
