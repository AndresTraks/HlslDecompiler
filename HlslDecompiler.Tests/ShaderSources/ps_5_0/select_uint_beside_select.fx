struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	uint t0 = 3 * i.idx;
	return float4(i.idx & 1 ? asfloat(t0) : t0 > 7 ? i.texcoord.x : i.texcoord.w, t0 > 7 ? i.texcoord.yz : i.texcoord.zy, (float)t0);
}
