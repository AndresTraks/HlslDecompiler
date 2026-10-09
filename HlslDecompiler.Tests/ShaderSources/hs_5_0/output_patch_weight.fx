float4x4 view;
float lod;

struct HS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
	float3 normal : NORMAL;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, OutputPatch<HS_OUT, 3> opatch)
{
	HS_CONST o;

	float t0 = 0.333333343 * (opatch[0].weight + opatch[1].weight + opatch[2].weight) * lod;
	float t1 = max(t0, 1);
	o.edges[0] = t1;
	o.edges[1] = t1;
	o.edges[2] = t1;
	o.inside = t1;

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

	o.position = patch[sv_outputcontrolpointid].position;
	o.weight = 1 / max(length(mul(float4(patch[sv_outputcontrolpointid].position, 1), (float4x3)view)), 0.00999999978);
	o.normal = normalize(patch[sv_outputcontrolpointid].normal);

	return o;
}
