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

	o.sv_position = i.blendweight * (float4)i.blendindices.x + asfloat(i.blendindices.w);
	o.out_ = i.blendweight.x > 0.5 ? asint(i.blendweight.y) : i.blendindices.y;

	return o;
}
