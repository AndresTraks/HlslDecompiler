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

	int4 r0;
	int4 r1;
	r0.xyz = asint(patch[1].position.xyz + patch[0].position.xyz);
	r0.xyz = asint(asfloat(r0.xyz) + patch[2].position.xyz);
	r1.xyz = asint(asfloat(r0.xyz) * float3(0.333333343, 0.333333343, 0.333333343));
	r0.w = asint(dot(frustum[0].xyz, asfloat(r1.xyz)));
	r0.w = asint(asfloat(r0.w) + frustum[0].w);
	r0.w = (-2 < asfloat(r0.w)) ? -1 : 0;
	r1.w = asint(dot(frustum[1].xyz, asfloat(r1.xyz)));
	r1.w = asint(asfloat(r1.w) + frustum[1].w);
	r1.w = (-2 < asfloat(r1.w)) ? -1 : 0;
	r0.w = r0.w & r1.w;
	r1.w = asint(dot(frustum[2].xyz, asfloat(r1.xyz)));
	r1.w = asint(asfloat(r1.w) + frustum[2].w);
	r1.w = (-2 < asfloat(r1.w)) ? -1 : 0;
	r0.w = r0.w & r1.w;
	r1.w = asint(dot(frustum[3].xyz, asfloat(r1.xyz)));
	r1.w = asint(asfloat(r1.w) + frustum[3].w);
	r1.w = (-2 < asfloat(r1.w)) ? -1 : 0;
	r0.w = r0.w & r1.w;
	if (r0.w == 0) {
		o.centre = asfloat(r1.xyz);
		o.edges[0] = 0;
		o.edges[1] = 0;
		o.edges[2] = 0;
		o.inside = 0;
		return o;
	}
	r0.xyz = asint(asfloat(r0.xyz) * float3(0.333333343, 0.333333343, 0.333333343) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(max(asfloat(r0.x), 0.00100000005));
	r0.x = asint(detail / asfloat(r0.x));
	o.centre = asfloat(r1.xyz);
	o.edges[0] = asfloat(r0.x);
	o.edges[1] = asfloat(r0.x);
	o.edges[2] = asfloat(r0.x);
	o.inside = asfloat(r0.x);

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
