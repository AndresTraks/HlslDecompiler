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

	o.texcoord = float2(dot(transpose(tm)[0].xyw, float3(i.texcoord, 1)), dot(transpose(tm)[1].xyw, float3(i.texcoord, 1)));
	o.position = i.position;

	return o;
}
