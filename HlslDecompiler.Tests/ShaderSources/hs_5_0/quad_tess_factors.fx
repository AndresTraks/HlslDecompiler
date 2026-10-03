cbuffer Params : register(b0)
{
	float3 eye;
	float detail;
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
	float3 centre : CENTRE;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	float3 t0 = patch[1].position + patch[0].position + patch[2].position + patch[3].position;
	float t1 = detail / max(length(0.25 * t0 - eye), 0.00100000005);
	float t2 = clamp(t1, 1, 64);
	float t3 = clamp(0.5 * t1, 1, 64);
	o.edges[0] = t2;
	o.centre = 0.25 * t0;
	o.edges[1] = t2;
	o.edges[2] = t2;
	o.edges[3] = t2;
	o.inside[0] = t3;
	o.inside[1] = t3;

	return o;
}

[domain("quad")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = patch[sv_outputcontrolpointid].position;
	o.texcoord = patch[sv_outputcontrolpointid].texcoord;

	return o;
}
