cbuffer Params : register(b0)
{
	float4 frustum[4];
	float3 eye;
	float detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

struct HS_OUT
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

struct HS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float3 centre : CENTRE;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float3 t1 = patch[1].position + patch[0].position + patch[2].position;
	float3 t0 = 0.333333343 * t1;
	float t2 = dot(frustum[0], float4(t0, 1)) > -2 && dot(frustum[1], float4(t0, 1)) > -2 && dot(frustum[2], float4(t0, 1)) > -2 && dot(frustum[3], float4(t0, 1)) > -2;
	if (t2 == 0) {
		o.edges[0] = 0;
		o.centre = t0;
		o.edges[1] = 0;
		o.edges[2] = 0;
		o.inside = 0;

		return o;
	}
	float t3 = max(length(0.333333343 * t1 - eye), 0.00100000005);
	o.edges[0] = detail / t3;
	o.centre = t0;
	o.edges[1] = detail / t3;
	o.edges[2] = detail / t3;
	o.inside = detail / t3;

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

	o.position = patch[sv_outputcontrolpointid].position;
	o.normal = patch[sv_outputcontrolpointid].normal;
	o.texcoord = patch[sv_outputcontrolpointid].texcoord;

	return o;
}
