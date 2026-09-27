cbuffer P : register(b0)
{
	float factor;
};

struct HS_IN
{
	float4 sv_position : SV_Position;
};

struct HS_OUT
{
	float4 sv_position : SV_Position;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float r0;
	r0 = 0;
	o.edges[0] = factor;
	r0 = 1;
	o.edges[1] = factor;
	r0 = 2;
	o.edges[2] = factor;
	o.inside = factor;

	return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.sv_position = patch[sv_outputcontrolpointid].sv_position;

	return o;
}
