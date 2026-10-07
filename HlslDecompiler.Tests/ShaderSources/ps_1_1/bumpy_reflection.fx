sampler2D s0;
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(s0, i.texcoord).xyz;
	float t1 = dot(i.texcoord1, 2 * t0 - 1);
	float t2 = dot(i.texcoord2, 2 * t0 - 1);
	float t3 = dot(i.texcoord3, 2 * t0 - 1);
	float t4 = 2 * ((0.300000012 * t1 + 0.600000024 * t2 - t3) / dot(float3(t1, t2, t3), float3(t1, t2, t3)));
	float4 t5 = tex2D(s3, float2(t1, t2) * t4 - float2(0.300000012, 0.600000024));
	return t5 * i.color;
}
