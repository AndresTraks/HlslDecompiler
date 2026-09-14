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
	float t2 = dot(transpose(invViewProj)[3], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1));
	float3 t1 = lightPos - float3(dot(transpose(invViewProj)[0], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1)), dot(transpose(invViewProj)[1], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1)), dot(transpose(invViewProj)[2], float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1))) / t2;
	float t0 = saturate(1 - length(t1) / lightRange);
	float t3 = 1 / length(t1);
	float t4 = saturate(dot(2 * normalTex.Sample(samp, texcoord).xyz - 1, t1 * t3));
	return t0 * t4 * albedoTex.Sample(samp, texcoord) * lightColour;
}
