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
	float4 t0 = float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1);
	float t1 = dot(transpose(invViewProj)[3], t0);
	float3 t2 = lightPos - float3(dot(transpose(invViewProj)[0], t0), dot(transpose(invViewProj)[1], t0), dot(transpose(invViewProj)[2], t0)) / t1;
	float t3 = saturate(1 - length(t2) / lightRange);
	float t4 = 1 / length(t2);
	float t5 = saturate(dot(2 * normalTex.Sample(samp, texcoord).xyz - 1, t2 * t4));
	return t3 * t5 * albedoTex.Sample(samp, texcoord) * lightColour;
}
