float lod;

struct HS_IN
{
	float4 pos : POS;
	float3 n : N;
};

struct HS_OUT
{
	float4 pos : POS;
	float3 n : N;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	float3 center : CENTER;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	int4 r0;
	r0.x = 0 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(0);
	r0.xzw = asint(-(patch[r0.x].pos.xyz) + patch[r0.y].pos.xyz);
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) * lod);
	o.edges[0] = asfloat(r0.x);
	r0.x = 1 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(1);
	r0.xzw = asint(-(patch[r0.x].pos.xyz) + patch[r0.y].pos.xyz);
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) * lod);
	o.edges[1] = asfloat(r0.x);
	r0.x = 2 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(2);
	r0.xzw = asint(-(patch[r0.x].pos.xyz) + patch[r0.y].pos.xyz);
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) * lod);
	o.edges[2] = asfloat(r0.x);
	r0.x = 3 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(3);
	r0.xzw = asint(-(patch[r0.x].pos.xyz) + patch[r0.y].pos.xyz);
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) * lod);
	o.edges[3] = asfloat(r0.x);
	r0.x = asint(patch[1].pos.x + patch[0].pos.x);
	r0.x = asint(asfloat(r0.x) + patch[2].pos.x);
	r0.x = asint(asfloat(r0.x) + patch[3].pos.x);
	o.center.x = asfloat(r0.x) * 0.25;
	r0.x = asint(patch[1].pos.y + patch[0].pos.y);
	r0.x = asint(asfloat(r0.x) + patch[2].pos.y);
	r0.x = asint(asfloat(r0.x) + patch[3].pos.y);
	o.center.y = asfloat(r0.x) * 0.25;
	r0.x = asint(patch[1].pos.z + patch[0].pos.z);
	r0.x = asint(asfloat(r0.x) + patch[2].pos.z);
	r0.x = asint(asfloat(r0.x) + patch[3].pos.z);
	o.center.z = asfloat(r0.x) * 0.25;
	r0.x = asint(0);
	r0.y = asint(max(o.edges[0].x, o.edges[2].x));
	o.inside[0] = asfloat(r0.y);
	r0.x = asint(1);
	r0.y = asint(max(o.edges[1].x, o.edges[3].x));
	o.inside[1] = asfloat(r0.y);

	return o;
}

[domain("quad")]
[partitioning("fractional_even")]
[outputtopology("triangle_ccw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	float2 r0;
	r0.x = sv_outputcontrolpointid;
	o.pos = patch[r0.x].pos;
	r0.y = dot(patch[r0.x].n.xyz, patch[r0.x].n.xyz);
	r0.y = rsqrt(r0.y);
	o.n = r0.yyy * patch[r0.x].n.xyz;

	return o;
}
