float4x4 wvp;
float4 threshold;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float t0 = any(threshold < i.color);
	float t1 = all(threshold < i.color);
	o.sv_position = mul(i.position, wvp);
	o.color = t0 ? t1 ? i.color : 0.5 * i.color : float4(0, 0, 0, 1);

	return o;
}
