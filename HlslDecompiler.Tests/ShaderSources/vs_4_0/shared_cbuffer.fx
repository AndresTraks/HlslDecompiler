float4x4 viewProj;
float3 right;
float3 up;
float size;

struct VS_IN
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
};

float4 main(VS_IN i) : SV_Position
{
	return float4(dot(transpose(viewProj)[0].xyz, (right * i.texcoord.x + i.texcoord.y * up) * size + i.position.xyz) + transpose(viewProj)[0].w, dot(transpose(viewProj)[1].xyz, (right * i.texcoord.x + i.texcoord.y * up) * size + i.position.xyz) + transpose(viewProj)[1].w, dot(transpose(viewProj)[2].xyz, (right * i.texcoord.x + i.texcoord.y * up) * size + i.position.xyz) + transpose(viewProj)[2].w, dot(transpose(viewProj)[3].xyz, (right * i.texcoord.x + i.texcoord.y * up) * size + i.position.xyz) + transpose(viewProj)[3].w);
}
