cbuffer Params : register(b0)
{
	float3 eye;
	float detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct HS_OUT
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	float3 centre : CENTRE;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	float3 r0;
	r0.x = patch[1].position.x + patch[0].position.x;
	r0.x = r0.x + patch[2].position.x;
	r0.x = r0.x + patch[3].position.x;
	o.centre.x = r0.x * 0.25;
	r0.x = patch[1].position.y + patch[0].position.y;
	r0.x = r0.x + patch[2].position.y;
	r0.x = r0.x + patch[3].position.y;
	o.centre.y = r0.x * 0.25;
	r0.x = patch[1].position.z + patch[0].position.z;
	r0.x = r0.x + patch[2].position.z;
	r0.x = r0.x + patch[3].position.z;
	o.centre.z = r0.x * 0.25;
	r0 = -(eye.xyz) + o.centre.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = max(r0.x, 0.00100000005);
	r0.x = detail / r0.x;
	r0.y = max(r0.x, 1);
	r0.x = r0.x * 0.5;
	r0.x = max(r0.x, 1);
	r0.xy = min(r0.xy, float2(64, 64));
	o.edges[0] = r0.y;
	o.edges[1] = r0.y;
	o.edges[2] = r0.y;
	o.edges[3] = r0.y;
	o.inside[0] = r0.x;
	o.inside[1] = r0.x;

	return o;
}

[domain("quad")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = patch[sv_outputcontrolpointid].position;
	o.texcoord = patch[sv_outputcontrolpointid].texcoord;

	return o;
}
