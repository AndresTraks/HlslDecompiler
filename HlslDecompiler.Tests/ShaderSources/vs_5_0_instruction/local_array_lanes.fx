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

	int2 r0;
	int4 x0[3];
	int4 x1[3];
	x0[0].x = i.sv_vertexid;
	r0.x = i.sv_vertexid * 7;
	x0[1].x = r0.x;
	x0[2].x = asint(i.position.x);
	x1[0].x = asint(i.position.y);
	x1[1].x = asint(i.position.z);
	x1[2].x = i.sv_vertexid;
	r0 = i.sv_vertexid + int2(1, 2);
	r0 = (uint2)r0.xy % int2(3, 3);
	o.sv_position.y = asfloat(x0[r0.x].x);
	o.color.y = x1[r0.y].x;
	r0.x = (uint)i.sv_vertexid % 3;
	o.sv_position.x = asfloat(x1[r0.x].x);
	o.color.x = x0[r0.x].x;
	o.sv_position.zw = i.position.zw;
	o.color.z = (uint)asint(i.position.x) >> 3;
	o.color.w = 1;

	return o;
}
