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

	float t0 = (float)((uint)i.color >> 24);
	o.sv_position = mul(float4(i.position, 1), worldViewProj);
	o.color = 0.00392156886 * t0 * 0.00392156886 * float4((float)(i.color & 255), (float2)(((uint2)i.color >> int2(8, 16)) & 255), t0);

	return o;
}
