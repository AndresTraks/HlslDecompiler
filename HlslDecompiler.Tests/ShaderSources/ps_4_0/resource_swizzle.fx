float4x4 invViewProj;
float3 lightPos;
float lightRange;
float4 lightColour;

SamplerState samp;
Texture2D albedoTex;
Texture2D normalTex;
Texture2D depthTex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float t3 = dot(transpose(invViewProj)[3], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1));
	float t1 = lightPos.x - dot(transpose(invViewProj)[0], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1)) / t3;
	float t2 = lightPos.y - dot(transpose(invViewProj)[1], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1)) / t3;
	float t4 = lightPos.z - dot(transpose(invViewProj)[2], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1)) / t3;
	float t5 = t1 * t1 + t2 * t2 + t4 * t4;
	float t0 = saturate(1 - sqrt(t5) / lightRange);
	float t6 = 1 / sqrt(t5);
	float t7 = saturate((2 * normalTex.Sample(samp, texcoord).x - 1) * t1 * t6 + (2 * normalTex.Sample(samp, texcoord).y - 1) * t2 * t6 + (2 * normalTex.Sample(samp, texcoord).z - 1) * t4 * t6);
	return t0 * t7 * albedoTex.Sample(samp, texcoord) * lightColour;
}
