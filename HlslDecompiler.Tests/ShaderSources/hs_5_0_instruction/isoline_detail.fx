cbuffer Params : register(b0)
{
	float4x4 world;
	float2 detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float texcoord : TEXCOORD;
};

struct HS_OUT
{
	float3 position : POSITION;
	float texcoord : TEXCOORD;
};

struct HS_CONST
{
	float edges[2] : SV_TessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 2> patch, uint sv_primitiveid : SV_PrimitiveID)
{
	HS_CONST o;

	float r0;
	r0 = patch[1].texcoord + patch[0].texcoord;
	o.edges[0] = r0.x * detail.x;
	r0 = (float)(uint)sv_primitiveid;
	o.edges[1] = r0.x + detail.y;

	return o;
}

[domain("isoline")]
[partitioning("fractional_even")]
[outputtopology("line")]
[outputcontrolpoints(2)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 2> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	float4 r0;
	r0.x = sv_outputcontrolpointid;
	o.texcoord = 0.5 * patch[r0.x].texcoord;
	r0.xyz = patch[r0.x].position.xyz;
	r0.w = 1;
	o.position.x = dot(r0, transpose(world)[0]);
	o.position.y = dot(r0, transpose(world)[1]);
	o.position.z = dot(r0, transpose(world)[2]);

	return o;
}
