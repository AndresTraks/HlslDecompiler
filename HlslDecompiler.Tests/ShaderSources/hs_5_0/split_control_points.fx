cbuffer Params : register(b0)
{
	float3 eye;
	float detail;
	float2 heightRange;
};

SamplerState heightSampler;
Texture2D heightMap;

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
};

HS_CONST constants(InputPatch<HS_IN, 3> patch, uint sv_primitiveid : SV_PrimitiveID)
{
	HS_CONST o;

	float3 t0 = patch[1].position + patch[0].position + patch[2].position;
	float t1 = max(length(0.333333343 * t0 - eye), 1);
	float t2 = heightMap.SampleLevel(heightSampler, 0.00333333341 * t0.xz, 0).x;
	float t3 = lerp(heightRange.x, heightRange.y, t2) * detail;
	o.edges[0] = t3 / t1;
	o.edges[1] = t3 / t1;
	o.edges[2] = t3 / t1;
	o.inside = (float)(sv_primitiveid & 1) + t3 / t1;

	return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(6)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 3> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	if (sv_outputcontrolpointid < 3) {
		o.position = patch[sv_outputcontrolpointid].position;
		o.normal = patch[sv_outputcontrolpointid].normal;
	} else {
		uint t0 = (sv_outputcontrolpointid - 2) % 3;
		int t1 = sv_outputcontrolpointid - 3;
		o.position = 0.5 * (patch[t0].position + patch[t1].position);
		o.normal = normalize(patch[t0].normal + patch[t1].normal);
	}

	return o;
}
