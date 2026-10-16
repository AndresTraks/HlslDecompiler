struct VS_IN
{
	int4 blendindices : BLENDINDICES;
	float4 blendweight : BLENDWEIGHT;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint out_ : OUT;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	int r0;
	r0 = asint((float)i.blendindices.x);
	o.sv_position = i.blendweight * asfloat(r0.x) + asfloat(i.blendindices.w);
	r0 = (0.5 < i.blendweight.x) ? -1 : 0;
	o.out_ = (r0.x != 0) ? asint(i.blendweight.y) : i.blendindices.y;

	return o;
}
