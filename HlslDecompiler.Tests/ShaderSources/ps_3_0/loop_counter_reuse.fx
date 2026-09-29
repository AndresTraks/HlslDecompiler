float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = rcp(k.y - k.x);
	float4 t1 = 0;
	float t2 = 0;
	for (int i = 0; i < 3; i++) {
		float4 t3 = smoothstep(0, 1, t0 * (texcoord * t2 - k.x)) * ((texcoord - t2 >= 0 ? 0 : -1) + (-(texcoord - t2) >= 0 ? 0 : 1)) + t1;
		float t4 = k.z - fmod(t3.x, k.w);
		t1 = t4 >= 0 ? t3 : clamp(t3, -k, k);
		t2 = t2 + 1;
	}
	return t1;
}
