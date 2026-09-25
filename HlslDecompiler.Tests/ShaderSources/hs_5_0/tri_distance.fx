cbuffer Params : register(b0)
{
	float3 cameraPosition;
	float tessScale;
};

struct HS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_OUT
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float t0 = tessScale / length(0.333333343 * (patch[1].position + patch[0].position + patch[2].position) - cameraPosition);
	o.edges[0] = clamp(t0, 1, 16);
	o.edges[1] = clamp(t0, 1, 16);
	o.edges[2] = clamp(t0, 1, 16);
	o.inside = clamp(t0, 1, 16);

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
	o.normal = normalize(patch[sv_outputcontrolpointid].normal);

	return o;
}
