struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.position = float4(dot(float4(2, 3, 5, 7), i.position), dot(float4(11, 13, 17, 19), i.position), dot(float4(23, 29, 31, 37), i.position), dot(float4(41, 43, 47, 53), i.position));
	o.texcoord = float4(dot(float3(59, 61, 67), i.texcoord.xyz), dot(float3(73, 79, 83), i.texcoord.xyz), dot(float3(97, 101, 103), i.texcoord.xyz), 107);

	return o;
}
