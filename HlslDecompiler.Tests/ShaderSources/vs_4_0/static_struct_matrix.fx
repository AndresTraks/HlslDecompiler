struct struct1
{
	float4x4 shadowMatrix;
	float4 colour;
};

struct1 lights[2];
float4x4 wvp;

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 texcoord : TEXCOORD;
	float4 color : COLOR;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.sv_position = mul(position, wvp);
	o.texcoord = mul(position, lights[1].shadowMatrix);
	o.color = lights[0].colour + lights[1].colour;

	return o;
}
