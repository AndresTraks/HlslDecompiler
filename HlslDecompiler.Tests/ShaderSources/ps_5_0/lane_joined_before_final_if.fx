struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float t0;
	if (i.idx == 3) {
		t0 = 7;
	} else {
		t0 = i.texcoord.y;
	}
	if (i.idx > 5) {
		return float4(i.texcoord.w, t0, i.texcoord.yw);
	} else {
		return float4(i.texcoord.x, t0, i.texcoord.z, 1);
	}
}
