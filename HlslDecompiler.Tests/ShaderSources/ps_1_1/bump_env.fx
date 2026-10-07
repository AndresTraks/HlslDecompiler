float4 bumpEnvMat1 : register(c9);
float4 bumpEnvMat2 : register(c10);
float2 bumpEnvLum2 : register(c18);
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
	float2 t2 = float2(dot(bumpEnvMat1.zx, t0.yx), dot(bumpEnvMat1.wy, t0.yx));
	float4 t3 = tex2D(s1, t2 + i.texcoord1);
	float2 t4 = float2(dot(bumpEnvMat2.zx, t0.yx), dot(bumpEnvMat2.wy, t0.yx));
	float2 t5 = t4 + i.texcoord2;
	float4 t6 = tex2D(s2, t5);
	return tex2D(s3, t0.yz) * i.color + t3 * float4(t6.xyz * t1, t6.w);
}
