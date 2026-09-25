cbuffer Params : register(b0)
{
	float3 cameraPosition;
	float tessScale;
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
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float3 r0;
	r0 = patch[1].position.xyz + patch[0].position.xyz;
	r0 = r0.xyz + patch[2].position.xyz;
	r0 = r0.xyz * float3(0.333333343, 0.333333343, 0.333333343) + -(cameraPosition.xyz);
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = tessScale / r0.x;
	r0.x = max(r0.x, 1);
	r0.x = min(r0.x, 16);
	o.edges[0] = r0.x;
	o.edges[1] = r0.x;
	o.edges[2] = r0.x;
	o.inside = r0.x;

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

	float2 r0;
	r0.x = sv_outputcontrolpointid;
	o.position = patch[r0.x].position.xyz;
	r0.y = dot(patch[r0.x].normal.xyz, patch[r0.x].normal.xyz);
	r0.y = rsqrt(r0.y);
	o.normal = r0.yyy * patch[r0.x].normal.xyz;

	return o;
}
