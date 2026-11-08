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
	float3 t0 = i.normal + i.tangent + i.binormal + i.position.xyz + float3(i.texcoord, i.texcoord1.x);
	return float4(t0, i.texcoord1.y + i.position.w) + i.color;
}
