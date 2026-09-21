cbuffer Lighting : register(b0)
{
	float4x4 lightViewProjection[3];
	float3 cascadeSplits;
	float shadowBias;
	float3 lightColor;
	float exposure;
};

SamplerState trilinear;
SamplerComparisonState shadowSampler;
TextureCube environment;
Texture2DArray shadowMaps;
Texture2D brdfLut;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float3 position : POSITION;
	float texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = i.sv_position.w < cascadeSplits.x ? 0 : i.sv_position.w < cascadeSplits.y ? 1 : 2;
	float t1 = dot(float4(i.position, 1), transpose(lightViewProjection[t0])[3]);
	float3 t2 = normalize(i.normal);
	float t3 = saturate(dot(t2, normalize(i.texcoord)));
	float2 t4 = brdfLut.Sample(trilinear, float2(t3, i.texcoord1)).xy;
	float t5 = t4.y + t4.x;
	float3 t6 = environment.SampleLevel(trilinear, t2, 6).xyz;
	float3 t7 = environment.SampleLevel(trilinear, reflect(-normalize(i.texcoord), t2), 6 * i.texcoord1).xyz;
	float3 t8 = mul(float4(i.position, 1), (float4x3)lightViewProjection[t0]) / t1;
	return float4(pow(1 - exp(-shadowMaps.SampleCmpLevelZero(shadowSampler, float3(float2(0.5, -0.5) * t8.xy + 0.5, (float)t0), t8.z - shadowBias).x * (t7 * t5 + t6) * lightColor * exposure), 0.454545468), 1);
}
