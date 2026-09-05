float4x4 bones[4];
float4x4 viewProj;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
};

float4 main(VS_IN i) : SV_Position
{
	return float4(dot(transpose(viewProj)[0].xy, float2(dot(transpose(bones[0])[0], i.position) * i.blendweight.x + dot(transpose(bones[1])[0], i.position) * i.blendweight.y, dot(transpose(bones[0])[1], i.position) * i.blendweight.x + dot(transpose(bones[1])[1], i.position) * i.blendweight.y)) + (dot(transpose(bones[0])[2], i.position) * i.blendweight.x + dot(transpose(bones[1])[2], i.position) * i.blendweight.y) * transpose(viewProj)[0].z + (dot(transpose(bones[0])[3], i.position) * i.blendweight.x + dot(transpose(bones[1])[3], i.position) * i.blendweight.y) * transpose(viewProj)[0].w, dot(transpose(viewProj)[1].xy, float2(dot(transpose(bones[0])[0], i.position) * i.blendweight.x + dot(transpose(bones[1])[0], i.position) * i.blendweight.y, dot(transpose(bones[0])[1], i.position) * i.blendweight.x + dot(transpose(bones[1])[1], i.position) * i.blendweight.y)) + (dot(transpose(bones[0])[2], i.position) * i.blendweight.x + dot(transpose(bones[1])[2], i.position) * i.blendweight.y) * transpose(viewProj)[1].z + (dot(transpose(bones[0])[3], i.position) * i.blendweight.x + dot(transpose(bones[1])[3], i.position) * i.blendweight.y) * transpose(viewProj)[1].w, dot(transpose(viewProj)[2].xy, float2(dot(transpose(bones[0])[0], i.position) * i.blendweight.x + dot(transpose(bones[1])[0], i.position) * i.blendweight.y, dot(transpose(bones[0])[1], i.position) * i.blendweight.x + dot(transpose(bones[1])[1], i.position) * i.blendweight.y)) + (dot(transpose(bones[0])[2], i.position) * i.blendweight.x + dot(transpose(bones[1])[2], i.position) * i.blendweight.y) * transpose(viewProj)[2].z + (dot(transpose(bones[0])[3], i.position) * i.blendweight.x + dot(transpose(bones[1])[3], i.position) * i.blendweight.y) * transpose(viewProj)[2].w, dot(transpose(viewProj)[3].xy, float2(dot(transpose(bones[0])[0], i.position) * i.blendweight.x + dot(transpose(bones[1])[0], i.position) * i.blendweight.y, dot(transpose(bones[0])[1], i.position) * i.blendweight.x + dot(transpose(bones[1])[1], i.position) * i.blendweight.y)) + (dot(transpose(bones[0])[2], i.position) * i.blendweight.x + dot(transpose(bones[1])[2], i.position) * i.blendweight.y) * transpose(viewProj)[3].z + (dot(transpose(bones[0])[3], i.position) * i.blendweight.x + dot(transpose(bones[1])[3], i.position) * i.blendweight.y) * transpose(viewProj)[3].w);
}
