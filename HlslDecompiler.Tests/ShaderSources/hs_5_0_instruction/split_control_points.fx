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

	int3 r0;
	float2 r1;
	r0 = asint(patch[1].position.xyz + patch[0].position.xyz);
	r0 = asint(asfloat(r0.xyz) + patch[2].position.xyz);
	r1 = asfloat(r0.xz) * float2(0.00333333341, 0.00333333341);
	r0 = asint(asfloat(r0.xyz) * float3(0.333333343, 0.333333343, 0.333333343) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(max(asfloat(r0.x), 1));
	r0.y = asint(heightMap.SampleLevel(heightSampler, r1.xy, 0).x);
	r0.z = asint(-(heightRange.x) + heightRange.y);
	r0.y = asint(asfloat(r0.y) * asfloat(r0.z) + heightRange.x);
	r0.y = asint(asfloat(r0.y) * detail);
	r0.x = asint(asfloat(r0.y) / asfloat(r0.x));
	o.edges[0] = asfloat(r0.x);
	o.edges[1] = asfloat(r0.x);
	o.edges[2] = asfloat(r0.x);
	r0.y = sv_primitiveid & 1;
	r0.y = asint((float)(uint)r0.y);
	o.inside = asfloat(r0.y) + asfloat(r0.x);

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

	float4 r0;
	float3 r1;
	r0.x = (sv_outputcontrolpointid < 3) ? -1 : 0;
	if (asint(r0.x) != 0) {
		r0.x = sv_outputcontrolpointid;
		o.position = patch[r0.x].position.xyz;
		o.normal = patch[r0.x].normal.xyz;
	} else {
		r0.xy = sv_outputcontrolpointid + int2(-3, -2);
		r0.y = (uint)r0.y % 3;
		r1 = patch[r0.y].position.xyz + patch[r0.x].position.xyz;
		o.position = r1.xyz * float3(0.5, 0.5, 0.5);
		r0.xyz = patch[r0.y].normal.xyz + patch[r0.x].normal.xyz;
		r0.w = dot(r0.xyz, r0.xyz);
		r0.w = rsqrt(r0.w);
		o.normal = r0.www * r0.xyz;
	}

	return o;
}
