float4x4 m;

struct VS_IN
{
	float4 position : POSITION;
	uint sv_vertexid : SV_VertexID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint id : ID;
	uint b : B;
	float f : F;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = mul(i.position, m);
	o.id = 3 * i.sv_vertexid + 1;
	o.b = i.position.x > 0 ? asint(i.position.y) : i.sv_vertexid;
	o.f = asfloat(i.sv_vertexid | 1065353216);

	return o;
}
