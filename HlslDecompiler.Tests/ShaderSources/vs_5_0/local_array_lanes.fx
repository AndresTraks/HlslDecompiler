struct VS_IN
{
	float4 position : POSITION;
	uint sv_vertexid : SV_VertexID;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	int4 x0[3];
	int4 x1[3];

	x0[0].x = i.sv_vertexid;
	x0[1].x = 7 * i.sv_vertexid;
	x0[2].x = asint(i.position.x);
	x1[0].x = asint(i.position.y);
	x1[1].x = asint(i.position.z);
	x1[2].x = i.sv_vertexid;
	o.sv_position = float4(asfloat(int2(x1[i.sv_vertexid % 3].x, x0[(i.sv_vertexid + 1) % 3].x)), i.position.zw);
	o.color = int4(x0[i.sv_vertexid % 3].x, x1[(i.sv_vertexid + 2) % 3].x, (uint)asint(i.position.x) >> 3, 1);

	return o;
}
