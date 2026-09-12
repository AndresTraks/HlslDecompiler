float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 3; i++) {
		t1 = t1 + 1;
		float t2 = saturate(rcp(k.y - k.x) * (texcoord.y * t1 - k.x));
		float t4 = saturate(rcp(k.y - k.x) * (texcoord.z * t1 - k.x));
		float t6 = saturate(rcp(k.y - k.x) * (texcoord.x * t1 - k.x));
		float t9 = saturate(rcp(k.y - k.x) * (texcoord.w * t1 - k.x));
		float t3 = t2 * t2 * (-2 * t2 + 3) * ((texcoord.y - t1 >= 0 ? 0 : -1) + (-(texcoord.y - t1) >= 0 ? 0 : 1)) + t0.y;
		float t5 = t4 * t4 * (-2 * t4 + 3) * ((texcoord.z - t1 >= 0 ? 0 : -1) + (-(texcoord.z - t1) >= 0 ? 0 : 1)) + t0.z;
		float t7 = t6 * t6 * (-2 * t6 + 3) * ((texcoord.x - t1 >= 0 ? 0 : -1) + (-(texcoord.x - t1) >= 0 ? 0 : 1)) + t0.x;
		float t8 = (rcp(k.w) * t7 >= 0 ? frac(abs(rcp(k.w) * t7)) : -frac(abs(rcp(k.w) * t7))) * -k.w + k.z;
		float t10 = t9 * t9 * (-2 * t9 + 3) * ((texcoord.w - t1 >= 0 ? 0 : -1) + (-(texcoord.w - t1) >= 0 ? 0 : 1)) + t0.w;
		t0 = float4(t8 >= 0 ? t7 : clamp(t7, -k.x, k.x), t8 >= 0 ? t3 : clamp(t3, -k.y, k.y), t8 >= 0 ? t5 : clamp(t5, -k.z, k.z), t8 >= 0 ? t10 : clamp(t10, -k.w, k.w));
	}
	return t0;
}
