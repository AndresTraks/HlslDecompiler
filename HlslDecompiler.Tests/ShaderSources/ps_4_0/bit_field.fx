uint4 packed;
float4 scale;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	uint t0 = ((uint)asint(texcoord.x) >> 23) & 255;
	int t1 = (asint(texcoord.x) & 8388607) + t0 * 8388608;
	float t2 = scale[(uint)texcoord.y & 3];
	uint t3 = 0;
	for (uint t4 = 0; t4 < 4; t4 = t4 + 1) {
		t3 = ((packed.x >> (t4 * 8)) & 255) > 128 ? t3 + 1 : t3;
	}
	return float4(t2 * asfloat(t1), (float)t3, (float)t0, 0);
}
