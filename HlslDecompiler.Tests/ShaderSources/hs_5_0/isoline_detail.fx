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

	o.edges[0] = (patch[1].texcoord + patch[0].texcoord) * detail.x;
	o.edges[1] = (float)sv_primitiveid + detail.y;

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

	o.position = mul(float4(patch[sv_outputcontrolpointid].position, 1), (float4x3)world);
	o.texcoord = 0.5 * patch[sv_outputcontrolpointid].texcoord;

	return o;
}
