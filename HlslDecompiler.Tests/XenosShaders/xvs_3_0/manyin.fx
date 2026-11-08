struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
	float3 binormal : BINORMAL;
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(VS_IN i) : POSITION
{
	return i.position + float4(i.normal + i.tangent + i.binormal, 0) + float4(i.texcoord, i.texcoord1) + i.color;
}
