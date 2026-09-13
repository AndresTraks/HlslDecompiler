float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float2 t0 = float2(rcp(k.y - k.x), rcp(k.w));
	float4 t1 = 0;
	float t2 = 0;
	for (int i = 0; i < 3; i++) {
		float4 t3 = saturate(t0.x * (texcoord.yzxw * t2 - k.x)) * saturate(t0.x * (texcoord.yzxw * t2 - k.x)) * (-2 * saturate(t0.x * (texcoord.yzxw * t2 - k.x)) + 3) * ((texcoord.yzxw - t2 >= 0 ? 0 : -1) + (-(texcoord.yzxw - t2) >= 0 ? 0 : 1)) + t1.yzxw;
		float t4 = t0.y * t3.z;
		float t5 = frac(abs(t4));
		float t6 = (t4 >= 0 ? t5 : -t5) * -k.w + k.z;
		t1 = t6 >= 0 ? t3.zxyw : clamp(t3.zxyw, -k, k);
		t2 = t2 + 1;
	}
	return t1;
}
