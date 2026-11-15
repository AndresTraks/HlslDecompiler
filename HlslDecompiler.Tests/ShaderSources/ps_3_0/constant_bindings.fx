float4 lightColor : register(c0);
float3x3 basis;
float2 scales[2];
float4 tint;
float4 fade;
float4 rim : register(c8);
sampler2D albedo;
sampler2D mask : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float t0 = dot(transpose(basis)[2], i.texcoord1);
	float t1 = saturate(dot(lightColor.xyz, float3(mul(i.texcoord1, (float3x2)basis), t0))) * lightColor.w;
	float2 t2 = i.texcoord * scales[0] + scales[1];
	float t3 = tex2D(mask, i.texcoord).x;
	float4 t4 = tex2D(albedo, t2) * tint;
	return rim * t0 + lerp(float4(t1 * t4.xyz, t4.w), fade, t3);
}
