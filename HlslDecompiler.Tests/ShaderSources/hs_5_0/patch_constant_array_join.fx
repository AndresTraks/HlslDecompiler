cbuffer C : register(b0)
{
	float3 eye;
	float k;
};

struct HS_IN
{
	float3 wpos : WPOS;
};

struct HS_OUT
{
	float3 wpos : WPOS;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	float lod : LOD;
	float3 corner[4] : CORNER;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	float t0 = k / length(0.5 * (patch[1].wpos + patch[0].wpos) - eye);
	float t1 = k / length(0.5 * (patch[2].wpos + patch[1].wpos) - eye);
	float t2 = k / length(0.5 * (patch[3].wpos + patch[2].wpos) - eye);
	float t3 = k / length(0.5 * (patch[0].wpos + patch[3].wpos) - eye);
	float t4 = clamp(t0, 1, 16);
	float t5 = clamp(t1, 1, 16);
	float t6 = clamp(t2, 1, 16);
	float t7 = clamp(t3, 1, 16);
	o.edges[0] = t4;
	o.lod = 0.25 * (t4 + t5 + t6 + t7);
	o.edges[1] = t5;
	o.edges[2] = t6;
	o.edges[3] = t7;
	o.corner[0] = float3(patch[0].wpos.x, patch[0].wpos.y, patch[0].wpos.z) - eye;
	o.corner[1] = float3(patch[1].wpos.x, patch[1].wpos.y, patch[1].wpos.z) - eye;
	o.corner[2] = float3(patch[2].wpos.x, patch[2].wpos.y, patch[2].wpos.z) - eye;
	o.corner[3] = float3(patch[3].wpos.x, patch[3].wpos.y, patch[3].wpos.z) - eye;
	o.inside[0] = 0.5 * (o.edges[1] + o.edges[3]);
	o.inside[1] = 0.5 * (o.edges[0] + o.edges[2]);

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

	o.wpos = patch[sv_outputcontrolpointid].wpos;

	return o;
}
