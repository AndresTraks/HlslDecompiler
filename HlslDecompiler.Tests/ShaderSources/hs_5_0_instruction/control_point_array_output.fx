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

	int r0;
	o.inside = 2;
	r0 = sv_primitiveid & 3;
	r0 = r0.x + 2;
	r0 = asint((float)(uint)r0.x);
	o.edges[0] = asfloat(r0.x);
	o.edges[1] = asfloat(r0.x);
	o.edges[2] = asfloat(r0.x);

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

	int2 r0;
	r0.x = asint(sv_outputcontrolpointid);
	o.wpos = patch[r0.x].wpos.xyz;
	o.weights[0] = dot(patch[r0.x].texcoord.xy, patch[0].texcoord.xy);
	r0.y = asint(dot(patch[r0.x].texcoord.xy, patch[1].texcoord.xy));
	r0.x = asint(dot(patch[r0.x].texcoord.xy, patch[2].texcoord.xy));
	o.weights[2] = asfloat(r0.x) + 2;
	o.weights[1] = asfloat(r0.y) + 1;
	o.pid = sv_primitiveid * 3 + sv_outputcontrolpointid;

	return o;
}
