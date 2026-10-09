float scale;

struct HS_IN
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct HS_OUT
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float3 b210 : B210;
	float3 b120 : B120;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float t0 = length(patch[1].position - patch[2].position);
	float t1 = length(patch[2].position - patch[0].position);
	float t2 = length(patch[0].position - patch[1].position);
	float t3 = t0 * scale;
	float t4 = t1 * scale;
	float t5 = t2 * scale;
	o.edges[0] = t3;
	o.b210 = 0.333333343 * (2 * patch[0].position + patch[1].position);
	o.edges[1] = t4;
	o.b120 = 0.333333343 * (2 * patch[1].position + patch[0].position);
	o.edges[2] = t5;
	o.inside = 0.333333343 * (t3 + t4 + t5);

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

	o.position = patch[sv_outputcontrolpointid].position;
	o.texcoord = patch[sv_outputcontrolpointid].texcoord + patch[sv_outputcontrolpointid].texcoord;

	return o;
}
