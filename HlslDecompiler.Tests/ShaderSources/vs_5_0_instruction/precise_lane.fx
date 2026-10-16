float4 a;
float4 b;

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	precise float4 r0;
	r0 = position * a;
	o.sv_position = r0 + b;
	o.color = position * a + b;

	return o;
}
