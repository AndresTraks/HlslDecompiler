float4x4 tm;

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float2 texcoord : TEXCOORD;
	float4 position : POSITION;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.texcoord = mul(float4(i.texcoord, 0, 1), tm).xy;
	o.position = i.position;

	return o;
}
