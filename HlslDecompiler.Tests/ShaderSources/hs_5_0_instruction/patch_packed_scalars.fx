static const float4 icb[4] =
{
	float4(6, 5, 2, 0),
	float4(0, 8, 3, 0),
	float4(0, 0, 4, 0),
	float4(0, 0, 7, 0),
};

struct HS_IN
{
	float4 color : COLOR;
};

struct HS_OUT
{
	float4 color : COLOR;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float a : A;
	float b : B;
	float2 c : C;
	float4 w : W;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	int2 r0;
	r0.x = asint(0);
	o.edges[0] = 1;
	r0.x = asint(1);
	o.edges[1] = 1;
	r0.x = asint(2);
	o.edges[2] = 1;
	o.inside = 1;
	r0.x = ((uint)0 < 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? asint(0) : 4;
	r0.y = asint(0);
	o.a = icb[r0.y].z;
	r0.x = ((uint)1 < 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? asint(1) : 4;
	r0.y = asint(1);
	o.b = icb[r0.y].z;
	r0.x = ((uint)2 < 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? asint(2) : 4;
	r0.y = asint(2);
	o.c.x = icb[r0.y].z;
	r0.x = ((uint)3 < 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? asint(3) : 4;
	r0.y = asint(3);
	o.w.y = icb[r0.y].z;
	r0.x = ((uint)0 < 1) ? -1 : 0;
	r0.x = (r0.x != 0) ? 0 : 2;
	r0.y = asint(0);
	o.c.y = icb[r0.y].y;
	r0.x = ((uint)1 < 1) ? -1 : 0;
	r0.x = (r0.x != 0) ? 0 : 2;
	r0.y = asint(1);
	o.w.z = icb[r0.y].y;
	o.w.x = 6;
	o.w.w = 9;

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

	o.color = patch[sv_outputcontrolpointid].color;

	return o;
}
