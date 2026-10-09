float lod;
uint count;

struct HS_IN
{
	float3 position : POSITION;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, OutputPatch<HS_OUT, 3> opatch)
{
	HS_CONST o;

	float3 r0;
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = ((uint)r0.y >= count) ? -1 : 0;
		if (asint(r0.z) != 0) break;
		r0.x = opatch[r0.y].weight * opatch[r0.y].position.y + r0.x;
		r0.y = r0.y + 1;
	}
	r0.x = r0.x * lod;
	r0.x = max(r0.x, 1);
	o.edges[0] = r0.x;
	o.inside = r0.x;
	r0.x = lod * opatch[1].position.x;
	o.edges[1] = max(r0.x, 1);
	o.edges[2] = max(1, opatch[2].weight);

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

	float2 r0;
	r0.x = (float)(uint)sv_outputcontrolpointid;
	r0.y = sv_outputcontrolpointid;
	o.weight = r0.x + patch[r0.y].position.z;
	o.position = patch[r0.y].position.xyz + patch[r0.y].position.xyz;

	return o;
}
