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

	float4 r0;
	r0.x = patch[1].position.x + patch[0].position.x;
	r0.x = r0.x + patch[2].position.x;
	o.centre.x = r0.x * 0.333333343;
	r0.x = patch[1].position.y + patch[0].position.y;
	r0.x = r0.x + patch[2].position.y;
	o.centre.y = r0.x * 0.333333343;
	r0.x = patch[1].position.z + patch[0].position.z;
	r0.x = r0.x + patch[2].position.z;
	o.centre.z = r0.x * 0.333333343;
	r0.xyz = -(eye.xyz) + o.centre.xyz;
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	r0.x = max(r0.x, 1);
	r0.yzw = -(patch[0].position.xyz) + o.centre.xyz;
	r0.y = dot(r0.yzw, r0.yzw);
	r0.y = sqrt(r0.y);
	r0.z = r0.y * detail;
	o.radius = r0.y;
	r0.x = r0.z / r0.x;
	o.edges[0] = r0.x;
	o.edges[1] = r0.x;
	o.edges[2] = r0.x;
	o.inside = r0.x * 0.5;

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

	float4 r0;
	float3 r1;
	r0.x = sv_outputcontrolpointid;
	r0.yzw = eye.xyz + -(patch[r0.x].position.xyz);
	r1.x = dot(r0.yzw, r0.yzw);
	r1.x = rsqrt(r1.x);
	r0.yzw = r0.yzw * r1.xxx;
	r1.x = dot(r0.yzw, patch[r0.x].normal.xyz);
	r0.yzw = r0.yzw * float3(0.25, 0.25, 0.25) + patch[r0.x].normal.xyz;
	r1 = r1.xxx * patch[r0.x].normal.xyz;
	o.position = r1.xyz * float3(0.100000001, 0.100000001, 0.100000001) + patch[r0.x].position.xyz;
	r0.x = dot(r0.yzw, r0.yzw);
	r0.x = rsqrt(r0.x);
	o.normal = r0.xxx * r0.yzw;

	return o;
}
