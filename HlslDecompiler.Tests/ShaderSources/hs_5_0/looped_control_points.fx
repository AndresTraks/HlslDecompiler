cbuffer Tessellation : register(b0)
{
	float edgeScale;
};

struct HS_IN
{
	float3 pos : POS;
};

struct HS_OUT
{
	float3 pos : POS;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	float t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		t0 = t0 + length(patch[t1].pos);
	}
	o.edges[0] = t0 * edgeScale;
	o.edges[1] = t0;
	o.edges[2] = t0;
	o.edges[3] = t0;
	o.inside[0] = t0;
	o.inside[1] = t0;

	return o;
}

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.pos = patch[sv_outputcontrolpointid].pos;

	return o;
}
