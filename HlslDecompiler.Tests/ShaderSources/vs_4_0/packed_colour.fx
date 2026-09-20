float4x4 worldViewProj;

struct VS_IN
{
	float3 position : POSITION;
	uint color : COLOR;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = mul(float4(i.position, 1), worldViewProj);
	o.color = 0.00392156886 * (float4)(i.color >> 24) * 0.00392156886 * float4((float)(i.color & 255), (float2)((i.color >> int2(8, 16)) & 255), (float)(i.color >> 24));

	return o;
}
