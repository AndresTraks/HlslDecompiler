struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(asfloat(i.idx == 3 ? 1088421888 : asint(i.texcoord.y)), (float)((uint)(i.idx == 3 ? 1088421888 : asint(i.texcoord.y)) >> 30), asfloat((i.idx == 3 ? 1088421888 : asint(i.texcoord.y)) ^ -2147483648), 1);
}
