sampler2D s0;
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = tex2D(s0, i.texcoord).xyz;
	float t1 = dot(i.texcoord1.xyz, 2 * t0 - 1);
	float t2 = dot(i.texcoord2.xyz, 2 * t0 - 1);
	float t3 = dot(i.texcoord3.xyz, 2 * t0 - 1);
	float t4 = 2 * ((t1 * i.texcoord1.w + t2 * i.texcoord2.w + t3 * i.texcoord3.w) / dot(float3(t1, t2, t3), float3(t1, t2, t3)));
	return tex2D(s3, float2(t1 * t4 - i.texcoord1.w, t2 * t4 - i.texcoord2.w));
}
