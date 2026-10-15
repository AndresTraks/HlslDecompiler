double s;

struct VS_OUT
{
	float4 sv_position : SV_Position;
	uint2 d : D;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	uint2 t0;
	asuint((double)position.x * s + 1, t0.x, t0.y);
	o.sv_position = position;
	o.d = t0;

	return o;
}
