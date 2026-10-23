struct HS_IN
{
	float3 wpos : WPOS;
	float2 texcoord : TEXCOORD;
};

struct HS_OUT
{
	float3 wpos : WPOS;
	float weights[3] : WEIGHTS;
	uint pid : PID;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, uint sv_primitiveid : SV_PrimitiveID)
{
	HS_CONST o;

	uint t0 = (sv_primitiveid & 3) + 2;
	float t1 = (float)t0;
	o.inside = 2;
	o.edges[0] = t1;
	o.edges[1] = t1;
	o.edges[2] = t1;

	return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_primitiveid : SV_PrimitiveID, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.wpos = patch[sv_outputcontrolpointid].wpos;
	o.weights[0] = dot(patch[sv_outputcontrolpointid].texcoord, patch[0].texcoord);
	o.pid = 3 * sv_primitiveid + sv_outputcontrolpointid;
	o.weights[1] = dot(patch[sv_outputcontrolpointid].texcoord, patch[1].texcoord) + 1;
	o.weights[2] = dot(patch[sv_outputcontrolpointid].texcoord, patch[2].texcoord) + 2;

	return o;
}
