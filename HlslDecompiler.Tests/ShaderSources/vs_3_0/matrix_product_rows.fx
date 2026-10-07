struct VS_OUT
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(dot(float4(2, 3, 5, 7), position), dot(float4(11, 13, 17, 19), position), dot(float4(23, 29, 31, 37), position), dot(float3(97, 101, 103), position.xyz));
	o.texcoord = float4(dot(float3(109, 113, 127), position.xyz), dot(float3(137, 139, 149), position.xyz), dot(float3(41, 43, 47), position.xyz), dot(float3(59, 61, 67), position.xyz));

	return o;
}
