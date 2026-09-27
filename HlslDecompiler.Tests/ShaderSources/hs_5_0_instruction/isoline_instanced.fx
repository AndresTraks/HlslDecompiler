static const float4 icb[2] =
{
	float4(4, 0, 0, 0),
	float4(8, 0, 0, 0),
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
	float edges[2] : SV_TessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 2> patch)
{
	HS_CONST o;

	float r0;
	r0 = 0;
	o.edges[0] = icb[r0.x].x;
	r0 = 1;
	o.edges[1] = icb[r0.x].x;

	return o;
}

[domain("isoline")]
[partitioning("integer")]
[outputtopology("line")]
[outputcontrolpoints(2)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 2> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.sv_position = patch[sv_outputcontrolpointid].sv_position;

	return o;
}
