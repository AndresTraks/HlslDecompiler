float lod;
uint count;

struct HS_IN
{
	float3 position : POSITION;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, OutputPatch<HS_OUT, 3> opatch)
{
	HS_CONST o;

	float t0 = 0;
	for (uint t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = t0 + opatch[t1].weight * opatch[t1].position.y;
	}
	float t2 = max(t0 * lod, 1);
	o.edges[0] = t2;
	o.inside = t2;
	o.edges[1] = max(lod * opatch[1].position.x, 1);
	o.edges[2] = max(1, opatch[2].weight);

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

	o.position = patch[sv_outputcontrolpointid].position + patch[sv_outputcontrolpointid].position;
	o.weight = (float)sv_outputcontrolpointid + patch[sv_outputcontrolpointid].position.z;

	return o;
}
