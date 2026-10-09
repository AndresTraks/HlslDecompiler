float4x4 view;
float lod;

struct HS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
	float3 normal : NORMAL;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, OutputPatch<HS_OUT, 3> opatch)
{
	HS_CONST o;

	float r0;
	r0 = opatch[1].weight + opatch[0].weight;
	r0 = r0.x + opatch[2].weight;
	r0 = r0.x * lod;
	r0 = r0.x * 0.333333343;
	r0 = max(r0.x, 1);
	o.edges[0] = r0.x;
	o.edges[1] = r0.x;
	o.edges[2] = r0.x;
	o.inside = r0.x;

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

	float4 r0;
	float r1;
	float3 r2;
	r0.w = 1;
	r1 = sv_outputcontrolpointid;
	r0.xyz = patch[r1.x].position.xyz;
	r2.x = dot(r0, transpose(view)[0]);
	r2.y = dot(r0, transpose(view)[1]);
	r2.z = dot(r0, transpose(view)[2]);
	r0.x = dot(r2.xyz, r2.xyz);
	r0.x = sqrt(r0.x);
	r0.x = max(r0.x, 0.00999999978);
	o.weight = 1 / r0.x;
	o.position = patch[r1.x].position.xyz;
	r0.x = dot(patch[r1.x].normal.xyz, patch[r1.x].normal.xyz);
	r0.x = rsqrt(r0.x);
	o.normal = r0.xxx * patch[r1.x].normal.xyz;

	return o;
}
