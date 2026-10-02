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

	o.position = float4(2, 11, 23, 41) * i.position.x + float4(3, 13, 29, 43) * i.position.y + float4(5, 17, 31, 47) * i.position.z + float4(7, 19, 37, 53) * i.position.w;
	o.texcoord = float4(float3(59, 73, 97) * i.texcoord.x + float3(61, 79, 101) * i.texcoord.y + float3(67, 83, 103) * i.texcoord.z, 107);

	return o;
}
