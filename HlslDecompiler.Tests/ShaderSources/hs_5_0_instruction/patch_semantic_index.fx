float scale;

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
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float3 b210 : B210;
	float3 b120 : B120;
};

HS_CONST constants(InputPatch<HS_IN, 3> patch)
{
	HS_CONST o;

	float3 r0;
	r0.xy = 0 + int2(2, 1);
	r0.xy = (uint2)r0.xy % int2(3, 3);
	r0 = -(patch[r0.x].position.xyz) + patch[r0.y].position.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = r0.x * scale;
	r0.y = 0;
	o.edges[0] = r0.x;
	r0.xy = 1 + int2(2, 1);
	r0.xy = (uint2)r0.xy % int2(3, 3);
	r0 = -(patch[r0.x].position.xyz) + patch[r0.y].position.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = r0.x * scale;
	r0.y = 1;
	o.edges[1] = r0.x;
	r0.xy = 2 + int2(2, 1);
	r0.xy = (uint2)r0.xy % int2(3, 3);
	r0 = -(patch[r0.x].position.xyz) + patch[r0.y].position.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = r0.x * scale;
	r0.y = 2;
	o.edges[2] = r0.x;
	r0.x = patch[0].position.x * 2 + patch[1].position.x;
	o.b210.x = r0.x * 0.333333343;
	r0.x = patch[0].position.y * 2 + patch[1].position.y;
	o.b210.y = r0.x * 0.333333343;
	r0.x = patch[0].position.z * 2 + patch[1].position.z;
	o.b210.z = r0.x * 0.333333343;
	r0.x = patch[1].position.x * 2 + patch[0].position.x;
	o.b120.x = r0.x * 0.333333343;
	r0.x = patch[1].position.y * 2 + patch[0].position.y;
	o.b120.y = r0.x * 0.333333343;
	r0.x = patch[1].position.z * 2 + patch[0].position.z;
	o.b120.z = r0.x * 0.333333343;
	r0.x = o.edges[0].x + o.edges[1].x;
	r0.x = r0.x + o.edges[2].x;
	o.inside = r0.x * 0.333333343;

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

	float r0;
	r0 = sv_outputcontrolpointid;
	o.position = patch[r0.x].position.xyz;
	o.texcoord = patch[r0.x].texcoord.xy + patch[r0.x].texcoord.xy;

	return o;
}
