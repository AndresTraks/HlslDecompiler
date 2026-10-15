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

	int r0;
	o.sv_position.x = dot(i.position, transpose(m)[0]);
	o.sv_position.y = dot(i.position, transpose(m)[1]);
	o.sv_position.z = dot(i.position, transpose(m)[2]);
	o.sv_position.w = dot(i.position, transpose(m)[3]);
	r0 = (0 < i.position.x) ? -1 : 0;
	o.b = (r0.x != 0) ? (int)i.position.y : i.sv_vertexid;
	o.id = i.sv_vertexid * 3 + 1;
	o.f = asfloat(i.sv_vertexid | 1065353216);

	return o;
}
