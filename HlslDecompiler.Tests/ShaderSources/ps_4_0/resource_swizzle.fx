cbuffer cb : register(b0)
{
	float4x4 invViewProj;
	float3 lightPos;
	float lightRange;
	float4 lightColour;
};

SamplerState samp;
Texture2D albedoTex;
Texture2D normalTex;
Texture2D depthTex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = float4(2 * texcoord - 1, depthTex.Sample(samp, texcoord).x, 1);
	float t1 = dot(transpose(invViewProj)[3], t0);
	float3 t2 = lightPos - mul(t0, (float4x3)invViewProj) / t1;
	float t3 = saturate(1 - length(t2) / lightRange);
	float3 t4 = normalTex.Sample(samp, texcoord).xyz;
	float t5 = saturate(dot(2 * t4 - 1, normalize(t2)));
	float4 t6 = albedoTex.Sample(samp, texcoord);
	return t3 * t5 * t6 * lightColour;
}
