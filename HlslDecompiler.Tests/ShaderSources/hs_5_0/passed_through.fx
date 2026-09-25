cbuffer Params : register(b0)
{
	float4 edgeFactors;
};

struct HS_IN
{
	float3 position : POSITION;
};

struct HS_OUT
{
	float3 position : POSITION;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	o.edges[0] = patch[0].position.x + edgeFactors.x;
	o.edges[1] = patch[1].position.x + edgeFactors.y;
	o.edges[2] = patch[2].position.x + edgeFactors.z;
	o.edges[3] = patch[3].position.x + edgeFactors.w;
	o.inside[0] = edgeFactors.x;
	o.inside[1] = edgeFactors.y;

	return o;
}

[domain("quad")]
[partitioning("pow2")]
[outputtopology("point")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = patch[sv_outputcontrolpointid].position;

	return o;
}
