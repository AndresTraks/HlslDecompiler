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
	float4 o;

	int4 r0;
	float4 r1;
	r0.x = i.idx & 3;
	r0.x = r0.x + 1;
	r0 = asint(tB[(r0.x - 1)]);
	r1 = tA;
	o = r1 * i.texcoord + asfloat(r0);

	return o;
}
