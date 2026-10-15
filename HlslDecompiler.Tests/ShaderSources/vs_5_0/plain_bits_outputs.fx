struct VS_IN
{
	float4 position : POSITION;
	uint sv_vertexid : SV_VertexID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint2 b : B;
	int s : S;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = i.position;
	o.b = uint2(asuint(i.position.x), i.sv_vertexid);
	o.s = asint(i.position.w);

	return o;
}
