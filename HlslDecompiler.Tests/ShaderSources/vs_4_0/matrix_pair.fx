float4x4 world;
float4x4 viewProj;

float4 main(float4 position : POSITION) : SV_Position
{
	return float4(dot(transpose(viewProj)[0].xy, float2(dot(transpose(world)[0], position), dot(transpose(world)[1], position))) + dot(transpose(world)[2], position) * transpose(viewProj)[0].z + dot(transpose(world)[3], position) * transpose(viewProj)[0].w, dot(transpose(viewProj)[1].xy, float2(dot(transpose(world)[0], position), dot(transpose(world)[1], position))) + dot(transpose(world)[2], position) * transpose(viewProj)[1].z + dot(transpose(world)[3], position) * transpose(viewProj)[1].w, dot(transpose(viewProj)[2].xy, float2(dot(transpose(world)[0], position), dot(transpose(world)[1], position))) + dot(transpose(world)[2], position) * transpose(viewProj)[2].z + dot(transpose(world)[3], position) * transpose(viewProj)[2].w, dot(transpose(viewProj)[3].xy, float2(dot(transpose(world)[0], position), dot(transpose(world)[1], position))) + dot(transpose(world)[2], position) * transpose(viewProj)[3].z + dot(transpose(world)[3], position) * transpose(viewProj)[3].w);
}
