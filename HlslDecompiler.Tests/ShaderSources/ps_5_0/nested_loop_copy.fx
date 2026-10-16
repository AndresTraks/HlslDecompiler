struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	uint2 t0 = uint2(i.idx & 3, (i.idx >> 2) & 3);
	float4 t1 = 0;
	for (uint t2 = 0; t2 < t0.x; t2 = t2 + 1) {
		float4 t3 = t1;
		uint t4 = t2;
		for (uint t5 = 0; t5 < t0.y; t5 = t5 + 1) {
			if (t4 == t5) {
				break;
			}
			t3 = t3 + i.texcoord * (float4)(t4 + t5);
		}
		t1 = t3 + float4(0, 0, 0, 1);
	}
	return t1;
}
