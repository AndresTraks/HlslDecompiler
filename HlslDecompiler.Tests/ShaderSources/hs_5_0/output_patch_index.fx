struct HS_IN
{
	float4 pos : POS;
};

struct HS_OUT
{
	float4 pos : POS;
	float w : W;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, OutputPatch<HS_OUT, 6> opatch)
{
	HS_CONST o;

	float t0 = opatch[3].w + opatch[0].w;
	float t1 = opatch[4].w + opatch[1].w;
	float t2 = opatch[5].w + opatch[2].w;
	o.edges[0] = t0;
	o.edges[1] = t1;
	o.edges[2] = t2;
	o.inside = 0.333333343 * (t0 + t1 + t2);

	return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(6)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	uint t0 = sv_outputcontrolpointid % 3;
	o.pos = (sv_outputcontrolpointid < 3 ? 1.0 : 0.5) * patch[t0].pos;
	o.w = (float)sv_outputcontrolpointid + patch[t0].pos.w;

	return o;
}
