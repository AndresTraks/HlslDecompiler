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

	float r0;
	o.edges[0] = min(edgeFactors.x, 32);
	o.edges[1] = min(edgeFactors.y, 32);
	o.edges[2] = min(edgeFactors.z, 32);
	o.edges[3] = min(edgeFactors.w, 32);
	r0 = insideFactors.x + patch[0].position.y;
	o.inside[0] = min(r0.x, 32);
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

	float r0;
	r0 = sv_outputcontrolpointid;
	o.position = patch[r0.x].position.xyz + patch[r0.x].position.xyz;
	o.texcoord = patch[r0.x].texcoord.xy;

	return o;
}
