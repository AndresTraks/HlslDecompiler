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

	int r0;
	r0 = asint(0);
	o.edges[0] = asfloat(patch[0].id);
	r0 = asint(1);
	o.edges[1] = asfloat(patch[0].id);
	r0 = asint(2);
	o.edges[2] = asfloat(patch[0].id);
	o.inside = (float)(uint)patch[1].id;
	r0 = (0 < patch[2].pos.x) ? -1 : 0;
	o.bits = (r0.x != 0) ? asint(patch[2].pos.y) : sv_primitiveid;

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

	int3 r0;
	r0.x = asint(sv_outputcontrolpointid);
	o.pos = patch[r0.x].pos;
	r0.y = (0 < patch[r0.x].pos.w) ? -1 : 0;
	r0.z = sv_outputcontrolpointid + patch[r0.x].id;
	o.id = (r0.y != 0) ? asint(patch[r0.x].pos.z) : r0.z;

	return o;
}
