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

	float2 r0;
	r0.x = 0;
	r0.y = opatch[r0.x + 3].w + opatch[r0.x].w;
	o.edges[0] = r0.y;
	r0.x = 1;
	r0.y = opatch[r0.x + 3].w + opatch[r0.x].w;
	o.edges[1] = r0.y;
	r0.x = 2;
	r0.y = opatch[r0.x + 3].w + opatch[r0.x].w;
	o.edges[2] = r0.y;
	r0.x = o.edges[0].x + o.edges[1].x;
	r0.x = r0.x + o.edges[2].x;
	o.inside = r0.x * 0.333333343;

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

	int2 r0;
	r0.x = (sv_outputcontrolpointid < 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? 1065353216 : 1056964608;
	r0.y = (uint)(int)sv_outputcontrolpointid % 3;
	o.pos = asfloat(r0.x) * patch[r0.y].pos;
	r0.x = asint((float)(uint)sv_outputcontrolpointid);
	o.w = asfloat(r0.x) + patch[r0.y].pos.w;

	return o;
}
