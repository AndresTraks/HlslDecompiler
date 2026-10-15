double s;

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint2 d : D;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	double d0;
	o.sv_position = position;
	d0 = (double)position.x;
	d0 = d0 * s;
	d0 = d0 + 1;
	asuint(d0, o.d.x, o.d.y);

	return o;
}
