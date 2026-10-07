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
	float t1 = dot(i.texcoord2, 2 * t0 - 1);
	float t2 = dot(i.texcoord3, 2 * t0 - 1);
	float3 t3 = 2 * t0 - 1;
	float t4 = dot(i.texcoord1, t3);
	return t4 * tex2D(s3, float2(t1, t2)) + i.color;
}
