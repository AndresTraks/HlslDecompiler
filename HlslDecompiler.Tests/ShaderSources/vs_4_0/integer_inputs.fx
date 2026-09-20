float4x4 wvp;
float4 palette[4];

struct VS_IN
{
	float4 position : POSITION;
	uint4 blendindices : BLENDINDICES;
	int texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = mul(i.position, wvp);
	o.color = palette[(i.blendindices.x >> 4) & 3] * 0.00392156886 * (float4)(i.blendindices.y & 255) + (float4)(i.texcoord + 1);

	return o;
}
