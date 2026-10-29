cbuffer C : register(b0)
{
	int mode;
};

struct DS_IN
{
	float4 edge[4] : EDGE;
	float4 color : COLOR;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float factor : FACTOR;
	float4 weights[4] : WEIGHTS;
};

[domain("tri")]
float4 main(DS_CONST constants, float sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	float4 o;

	int4 r0;
	r0.x = (uint)sv_domainlocation.x;
	r0.x = r0.x * 3;
	r0.x = r0.x & 3;
	r0.yz = mode & int2(3, 1);
	r0 = asint(constants.weights[r0.x] * constants.factor.x + patch[r0.z].edge[r0.y]);
	o = asfloat(r0) + patch[1].color;

	return o;
}
