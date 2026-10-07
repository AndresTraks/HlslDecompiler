float2x2 bumpEnvMat1 : register(c10);
float2x2 bumpEnvMat2 : register(c12);
float2 bumpEnvLum2 : register(c22);
sampler2D s0;
sampler2D s1;
sampler2D s2;
sampler2D s3;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(s0, i.texcoord).xyz;
	float t1 = saturate(t0.z * bumpEnvLum2.x + bumpEnvLum2.y);
	float2 t2 = mul(t0.xy, bumpEnvMat2) + i.texcoord2;
	float4 t3 = tex2D(s2, t2);
	float4 t4 = tex2D(s1, mul(t0.xy, bumpEnvMat1) + i.texcoord1);
	return tex2D(s3, t0.yz) * i.color + t4 * float4(t3.xyz * t1, t3.w);
}
