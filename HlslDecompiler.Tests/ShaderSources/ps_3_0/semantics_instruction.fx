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

	o.color.w = (i.vface >= 0) ? 1 : -1;
	o.color.xyz = i.vpos.xxy * float3(0, 1, 1) + float3(0.3, 0, 0);
	o.depth.xyzw = -123456;

	return o;
}
