struct struct1
{
	float4x4 shadowMatrix;
	float4 colour;
};

cbuffer Lights : register(b0)
{
	struct1 lights[2];
	float4x4 wvp;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 texcoord : TEXCOORD;
	float4 color : COLOR;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.sv_position.x = dot(position, transpose(wvp)[0]);
	o.sv_position.y = dot(position, transpose(wvp)[1]);
	o.sv_position.z = dot(position, transpose(wvp)[2]);
	o.sv_position.w = dot(position, transpose(wvp)[3]);
	o.texcoord.x = dot(position, transpose(lights[1].shadowMatrix)[0]);
	o.texcoord.y = dot(position, transpose(lights[1].shadowMatrix)[1]);
	o.texcoord.z = dot(position, transpose(lights[1].shadowMatrix)[2]);
	o.texcoord.w = dot(position, transpose(lights[1].shadowMatrix)[3]);
	o.color = lights[0].colour + lights[1].colour;

	return o;
}
