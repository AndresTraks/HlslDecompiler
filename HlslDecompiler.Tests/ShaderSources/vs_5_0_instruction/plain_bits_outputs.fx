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
	o.b.x = asint(i.position.x);
	o.s = asint(i.position.w);
	o.b.y = i.sv_vertexid;

	return o;
}
