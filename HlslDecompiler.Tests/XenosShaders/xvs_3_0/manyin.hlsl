struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float4 tangent : TANGENT;
	float4 binormal : BINORMAL;
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(VS_IN i) : POSITION
{
	float3 t0 = i.normal.xyz + i.tangent.xyz + i.binormal.xyz + i.position.xyz + float3(i.texcoord.xy, i.texcoord1.x);
	return float4(t0, i.texcoord1.y + i.position.w) + i.color;
}
