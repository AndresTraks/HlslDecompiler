struct PS_IN
{
	float vface : VFACE;
	float2 vpos : VPOS;
};

struct PS_OUT
{
	float4 color : COLOR;
	float depth : DEPTH;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.color = float4(0.300000012, i.vpos, i.vface >= 0 ? 1 : -1);
	o.depth = -123456;

	return o;
}
