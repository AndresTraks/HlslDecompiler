cbuffer Params : register(b0)
{
	float4 edgeFactors;
	float2 insideFactors;
};

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
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	o.edges[0] = min(edgeFactors.x, 32);
	o.edges[1] = min(edgeFactors.y, 32);
	o.edges[2] = min(edgeFactors.z, 32);
	o.edges[3] = min(edgeFactors.w, 32);
	o.inside[0] = min(patch[0].position.y + insideFactors.x, 32);
	o.inside[1] = min(insideFactors.y, 32);

	return o;
}

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_ccw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = patch[sv_outputcontrolpointid].position + patch[sv_outputcontrolpointid].position;
	o.texcoord = patch[sv_outputcontrolpointid].texcoord;

	return o;
}
