struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	if (i.idx == -1) {
		return float4(1, i.texcoord.yzw);
	} else {
		float4 t0;
		if (i.idx == 3) {
			t0 = float4(i.texcoord.x, 2, i.texcoord.zw);
		} else {
			if (i.idx == 4) return 0;
			t0 = i.texcoord.wzyx;
		}
		return t0;
	}
}
