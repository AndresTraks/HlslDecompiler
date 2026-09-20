uint4 packed;
float4 scale;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	int t0 = ((uint)asint(texcoord.x) >> 23) & 255;
	float2 t1 = float2((asint(texcoord.x) & 8388607) + (t0 * 8388608), dot(scale, icb[(uint)texcoord.y & 3]));
	int t2 = 0;
	for (uint t3 = 0; t3 < 4; t3 = t3 + 1) {
		t2 = t2 - (((packed.x >> (t3 * 8)) & 255) > 128 ? -1 : 0);
	}
	return float4(t1.y * t1.x, (float)t2, (float)t0, 0);
}
