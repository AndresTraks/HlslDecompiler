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

	precise float t0 = position.x * a.x + b.x;
	o.sv_position = float4(t0, position.yzw * a.yzw + b.yzw);
	o.color = position * a + b;

	return o;
}
