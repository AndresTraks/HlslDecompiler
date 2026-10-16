tbuffer TB : register(t2)
{
	float4 tA;
	float4 tB[4];
};

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	return tA * i.texcoord + tB[(i.idx & 3) + 1 - 1];
}
