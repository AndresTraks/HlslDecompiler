cbuffer C : register(b0)
{
	float3 eye;
	float k;
};

struct HS_IN
{
	float3 wpos : WPOS;
};

struct HS_OUT
{
	float3 wpos : WPOS;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	float lod : LOD;
	float3 corner[4] : CORNER;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	int4 r0;
	r0.x = 0 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(0);
	r0.xzw = asint(patch[r0.x].wpos.xyz + patch[r0.y].wpos.xyz);
	r0.xzw = asint(asfloat(r0.xzw) * float3(0.5, 0.5, 0.5) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(k / asfloat(r0.x));
	r0.x = asint(max(asfloat(r0.x), 1));
	r0.x = asint(min(asfloat(r0.x), 16));
	o.edges[0] = asfloat(r0.x);
	r0.x = 1 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(1);
	r0.xzw = asint(patch[r0.x].wpos.xyz + patch[r0.y].wpos.xyz);
	r0.xzw = asint(asfloat(r0.xzw) * float3(0.5, 0.5, 0.5) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(k / asfloat(r0.x));
	r0.x = asint(max(asfloat(r0.x), 1));
	r0.x = asint(min(asfloat(r0.x), 16));
	o.edges[1] = asfloat(r0.x);
	r0.x = 2 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(2);
	r0.xzw = asint(patch[r0.x].wpos.xyz + patch[r0.y].wpos.xyz);
	r0.xzw = asint(asfloat(r0.xzw) * float3(0.5, 0.5, 0.5) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(k / asfloat(r0.x));
	r0.x = asint(max(asfloat(r0.x), 1));
	r0.x = asint(min(asfloat(r0.x), 16));
	o.edges[2] = asfloat(r0.x);
	r0.x = 3 + 1;
	r0.x = r0.x & 3;
	r0.y = asint(3);
	r0.xzw = asint(patch[r0.x].wpos.xyz + patch[r0.y].wpos.xyz);
	r0.xzw = asint(asfloat(r0.xzw) * float3(0.5, 0.5, 0.5) + -(eye.xyz));
	r0.x = asint(dot(asfloat(r0.xzw), asfloat(r0.xzw)));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.x = asint(k / asfloat(r0.x));
	r0.x = asint(max(asfloat(r0.x), 1));
	r0.x = asint(min(asfloat(r0.x), 16));
	o.edges[3] = asfloat(r0.x);
	r0.x = asint(0);
	r0.y = asint(-(eye.x) + patch[r0.x].wpos.x);
	o.corner[0].x = asfloat(r0.y);
	r0.x = asint(1);
	r0.y = asint(-(eye.x) + patch[r0.x].wpos.x);
	o.corner[1].x = asfloat(r0.y);
	r0.x = asint(2);
	r0.y = asint(-(eye.x) + patch[r0.x].wpos.x);
	o.corner[2].x = asfloat(r0.y);
	r0.x = asint(3);
	r0.y = asint(-(eye.x) + patch[r0.x].wpos.x);
	o.corner[3].x = asfloat(r0.y);
	r0.x = asint(0);
	r0.y = asint(-(eye.y) + patch[r0.x].wpos.y);
	o.corner[0].y = asfloat(r0.y);
	r0.x = asint(1);
	r0.y = asint(-(eye.y) + patch[r0.x].wpos.y);
	o.corner[1].y = asfloat(r0.y);
	r0.x = asint(2);
	r0.y = asint(-(eye.y) + patch[r0.x].wpos.y);
	o.corner[2].y = asfloat(r0.y);
	r0.x = asint(3);
	r0.y = asint(-(eye.y) + patch[r0.x].wpos.y);
	o.corner[3].y = asfloat(r0.y);
	r0.x = asint(0);
	r0.y = asint(-(eye.z) + patch[r0.x].wpos.z);
	o.corner[0].z = asfloat(r0.y);
	r0.x = asint(1);
	r0.y = asint(-(eye.z) + patch[r0.x].wpos.z);
	o.corner[1].z = asfloat(r0.y);
	r0.x = asint(2);
	r0.y = asint(-(eye.z) + patch[r0.x].wpos.z);
	o.corner[2].z = asfloat(r0.y);
	r0.x = asint(3);
	r0.y = asint(-(eye.z) + patch[r0.x].wpos.z);
	o.corner[3].z = asfloat(r0.y);
	r0.x = -(0) + 1;
	r0.x = asint(o.edges[1].x + o.edges[3].x);
	r0.x = asint(asfloat(r0.x) * 0.5);
	r0.y = asint(0);
	o.inside[0] = asfloat(r0.x);
	r0.x = -(1) + 1;
	r0.x = asint(o.edges[0].x + o.edges[2].x);
	r0.x = asint(asfloat(r0.x) * 0.5);
	r0.y = asint(1);
	o.inside[1] = asfloat(r0.x);
	r0.x = asint(o.edges[0].x + o.edges[1].x);
	r0.x = asint(asfloat(r0.x) + o.edges[2].x);
	r0.x = asint(asfloat(r0.x) + o.edges[3].x);
	o.lod = asfloat(r0.x) * 0.25;

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

	o.wpos = patch[sv_outputcontrolpointid].wpos;

	return o;
}
