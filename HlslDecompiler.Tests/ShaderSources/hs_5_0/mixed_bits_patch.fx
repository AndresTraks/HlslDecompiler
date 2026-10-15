struct HS_IN
{
	float4 pos : POS;
	uint id : ID;
};

struct HS_OUT
{
	float4 pos : POS;
	uint id : ID;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	uint bits : BITS;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, uint sv_primitiveid : SV_PrimitiveID)
{
	HS_CONST o;

	o.edges[0] = asfloat(patch[0].id);
	o.bits = patch[2].pos.x > 0 ? asint(patch[2].pos.y) : sv_primitiveid;
	o.edges[1] = asfloat(patch[0].id);
	o.edges[2] = asfloat(patch[0].id);
	o.inside = (float)patch[1].id;

	return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.pos = patch[sv_outputcontrolpointid].pos;
	o.id = patch[sv_outputcontrolpointid].pos.w > 0 ? asint(patch[sv_outputcontrolpointid].pos.z) : sv_outputcontrolpointid + patch[sv_outputcontrolpointid].id;

	return o;
}
