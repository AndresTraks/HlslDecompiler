float lod;

struct HS_IN
{
	float4 pos : POS;
	float3 n : N;
};

struct HS_OUT
{
	float4 pos : POS;
	float3 n : N;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	float3 center : CENTER;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	o.edges[0] = length(patch[0].pos.xyz - patch[1].pos.xyz) * lod;
	o.center = 0.25 * (patch[0].pos.xyz + patch[1].pos.xyz + patch[2].pos.xyz + patch[3].pos.xyz);
	o.edges[1] = length(patch[1].pos.xyz - patch[2].pos.xyz) * lod;
	o.edges[2] = length(patch[2].pos.xyz - patch[3].pos.xyz) * lod;
	o.edges[3] = length(patch[3].pos.xyz - patch[0].pos.xyz) * lod;
	o.inside[0] = max(o.edges[0], o.edges[2]);
	o.inside[1] = max(o.edges[1], o.edges[3]);

	return o;
}

[domain("quad")]
[partitioning("fractional_even")]
[outputtopology("triangle_ccw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.pos = patch[sv_outputcontrolpointid].pos;
	o.n = normalize(patch[sv_outputcontrolpointid].n);

	return o;
}
