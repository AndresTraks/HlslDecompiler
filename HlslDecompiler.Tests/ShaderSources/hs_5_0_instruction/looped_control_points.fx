cbuffer Tessellation : register(b0)
{
	float edgeScale;
};

struct HS_IN
{
	float3 pos : POS;
};

struct HS_OUT
{
	float3 pos : POS;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	float3 r0;
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = (r0.y >= 4) ? -1 : 0;
		if (asint(r0.z) != 0) break;
		r0.z = dot(patch[r0.y].pos.xyz, patch[r0.y].pos.xyz);
		r0.z = sqrt(r0.z);
		r0.x = r0.z + r0.x;
		r0.y = r0.y + 1;
	}
	o.edges[0] = r0.x * edgeScale;
	o.edges[1] = r0.x;
	o.edges[2] = r0.x;
	o.edges[3] = r0.x;
	o.inside[0] = r0.x;
	o.inside[1] = r0.x;

	return o;
}

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.pos = patch[sv_outputcontrolpointid].pos;

	return o;
}
