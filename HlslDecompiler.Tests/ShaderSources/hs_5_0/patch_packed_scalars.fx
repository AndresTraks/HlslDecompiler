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

	o.edges[0] = 1;
	o.a = icb[0].z;
	o.edges[1] = 1;
	o.b = icb[1].z;
	o.edges[2] = 1;
	o.c = float2(icb[2].z, icb[0].y);
	o.inside = 1;
	o.w = float4(6, icb[3].z, icb[1].y, 9);

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
