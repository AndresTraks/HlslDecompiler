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

	int3 r0;
	r0.x = asint(0);
	o.edges[0] = 1;
	r0.x = asint(1);
	o.edges[1] = 1;
	r0.x = asint(2);
	o.edges[2] = 1;
	o.inside = 1;
	r0.x = asint(0);
	o.weights[0].x = icb[r0.x].w;
	r0.x = asint(1);
	o.weights[1].x = icb[r0.x].w;
	r0.x = asint(2);
	o.weights[2].x = icb[r0.x].w;
	r0.x = asint(3);
	o.weights[3].x = icb[r0.x].w;
	r0.x = ((uint)0 < 2) ? -1 : 0;
	r0.yz = 0 + int2(4, 3);
	r0.y = (uint)r0.y % 5;
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = asint(0);
	o.weights[0].y = icb[r0.y].z;
	r0.x = ((uint)1 < 2) ? -1 : 0;
	r0.yz = 1 + int2(4, 3);
	r0.y = (uint)r0.y % 5;
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = asint(1);
	o.factor = icb[r0.y].z;
	r0.x = ((uint)2 < 2) ? -1 : 0;
	r0.yz = 2 + int2(4, 3);
	r0.y = (uint)r0.y % 5;
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = asint(2);
	o.weights[1].y = icb[r0.y].z;
	r0.x = ((uint)3 < 2) ? -1 : 0;
	r0.yz = 3 + int2(4, 3);
	r0.y = (uint)r0.y % 5;
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = asint(3);
	o.weights[2].y = icb[r0.y].z;
	r0.x = ((uint)4 < 2) ? -1 : 0;
	r0.yz = 4 + int2(4, 3);
	r0.y = (uint)r0.y % 5;
	r0.x = (r0.x != 0) ? r0.y : r0.z;
	r0.y = asint(4);
	o.weights[3].y = icb[r0.y].z;
	r0.x = asint(0);
	o.weights[0].z = icb[r0.x].y;
	r0.x = asint(1);
	o.weights[1].z = icb[r0.x].y;
	r0.x = asint(2);
	o.weights[2].z = icb[r0.x].y;
	r0.x = asint(3);
	o.weights[3].z = icb[r0.x].y;
	r0.x = asint(0);
	o.weights[0].w = icb[r0.x].x;
	r0.x = asint(1);
	o.weights[1].w = icb[r0.x].x;
	r0.x = asint(2);
	o.weights[2].w = icb[r0.x].x;
	r0.x = asint(3);
	o.weights[3].w = icb[r0.x].x;

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
