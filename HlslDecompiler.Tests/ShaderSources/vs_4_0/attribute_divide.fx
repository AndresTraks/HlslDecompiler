float4x4 worldViewProj;
uint columns;

struct VS_IN
{
	float3 position : POSITION;
	uint texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.sv_position = mul(float4(i.position, 1), worldViewProj);
	o.texcoord = float2((float)(i.texcoord % columns), (float)(i.texcoord / columns)) / (float2)columns;

	return o;
}
