static const float4 icb[5] =
{
	float4(0, 0, 0, 1),
	float4(0, 0, 2, 0),
	float4(0, 1, 1, 0),
	float4(1, 0, 0, 0),
	float4(0, 0, 0, 0),
};

struct HS_IN
{
	float4 edge : EDGE;
	float4 edge1 : EDGE1;
	float4 edge2 : EDGE2;
	float4 edge3 : EDGE3;
	float4 color : COLOR;
};

struct HS_OUT
{
	float4 edge : EDGE;
	float4 edge1 : EDGE1;
	float4 edge2 : EDGE2;
	float4 edge3 : EDGE3;
	float4 color : COLOR;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float factor : FACTOR;
	float4 weights[4] : WEIGHTS;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	o.edges[0] = 1;
	o.factor = icb[1].z;
	o.edges[1] = 1;
	o.edges[2] = 1;
	o.inside = 1;
	o.weights[0] = float4(icb[0].w, icb[0].z, icb[0].y, icb[0].x);
	o.weights[1] = float4(icb[1].w, icb[2].z, icb[1].y, icb[1].x);
	o.weights[2] = float4(icb[2].w, icb[3].z, icb[2].y, icb[2].x);
	o.weights[3] = float4(icb[3].w, icb[4].z, icb[3].y, icb[3].x);

	return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.edge = patch[sv_outputcontrolpointid].edge;
	o.edge1 = patch[sv_outputcontrolpointid].edge1;
	o.edge2 = patch[sv_outputcontrolpointid].edge2;
	o.edge3 = patch[sv_outputcontrolpointid].edge3;
	o.color = patch[sv_outputcontrolpointid].color;

	return o;
}
