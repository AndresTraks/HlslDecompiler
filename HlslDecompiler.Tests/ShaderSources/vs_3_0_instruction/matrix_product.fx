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

	o.position = float4(dot(i.position, float4(2, 3, 5, 7)), dot(i.position, float4(11, 13, 17, 19)), dot(i.position, float4(23, 29, 31, 37)), dot(i.position, float4(41, 43, 47, 53)));
	o.texcoord.xyz = float3(dot(i.texcoord.xyz, float3(59, 61, 67)), dot(i.texcoord.xyz, float3(73, 79, 83)), dot(i.texcoord.xyz, float3(97, 101, 103)));
	o.texcoord.w = 107;

	return o;
}
