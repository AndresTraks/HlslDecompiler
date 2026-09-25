cbuffer Params : register(b0)
{
	float3 eye;
	float detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_OUT
{
	float3 position : POSITION;
	float3 normal : NORMAL;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float3 centre : CENTRE;
	float radius : RADIUS;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float3 t0 = patch[1].position + patch[0].position + patch[2].position;
	float t1 = max(length(0.333333343 * t0 - eye), 1);
	float3 t2 = 0.333333343 * t0 - patch[0].position;
	float t3 = length(t2) * detail;
	o.edges[0] = t3 / t1;
	o.centre = 0.333333343 * t0;
	o.edges[1] = t3 / t1;
	o.radius = length(t2);
	o.edges[2] = t3 / t1;
	o.inside = 0.5 * (t3 / t1);

	return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	float3 t0 = normalize(eye - patch[sv_outputcontrolpointid].position);
	float t1 = dot(t0, patch[sv_outputcontrolpointid].normal);
	o.position = 0.100000001 * t1 * patch[sv_outputcontrolpointid].normal + patch[sv_outputcontrolpointid].position;
	o.normal = normalize(0.25 * t0 + patch[sv_outputcontrolpointid].normal);

	return o;
}
