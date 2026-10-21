cbuffer C : register(b0)
{
	float detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	int2 r0;
	r0.x = 0 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(0);
	r0.x = asint(max(patch[r0.x].weight, patch[r0.y].weight));
	r0.x = asint(asfloat(r0.x) * detail);
	o.edges[0] = asfloat(r0.x);
	r0.x = 1 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(1);
	r0.x = asint(max(patch[r0.x].weight, patch[r0.y].weight));
	r0.x = asint(asfloat(r0.x) * detail);
	o.edges[1] = asfloat(r0.x);
	r0.x = 2 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(2);
	r0.x = asint(max(patch[r0.x].weight, patch[r0.y].weight));
	r0.x = asint(asfloat(r0.x) * detail);
	o.edges[2] = asfloat(r0.x);
	r0.x = 3 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(3);
	r0.x = asint(max(patch[r0.x].weight, patch[r0.y].weight));
	r0.x = asint(asfloat(r0.x) * detail);
	o.edges[3] = asfloat(r0.x);
	r0.x = asint(0);
	r0.y = asint(o.edges[0].x + o.edges[2].x);
	r0.y = asint(asfloat(r0.y) * 0.5);
	o.inside[0] = asfloat(r0.y);
	r0.x = asint(1);
	r0.y = asint(o.edges[1].x + o.edges[3].x);
	r0.y = asint(asfloat(r0.y) * 0.5);
	o.inside[1] = asfloat(r0.y);

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

	float r0;
	r0 = sv_outputcontrolpointid;
	o.position = float3(2, 2, 2) * patch[r0.x].position.xyz;
	o.weight = 1 * patch[r0.x].weight;

	return o;
}
