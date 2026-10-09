float4x4 bones[20];
sampler2D heightMap;
float heightScale : register(c84);
float2 heightUv : register(c85);
float time : register(c86);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
	float4 texcoord : TEXCOORD;
	float4 blendweight : BLENDWEIGHT;
	int4 blendindices : BLENDINDICES;
};

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float fog : FOG;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 t0 = mul(i.position, bones[i.blendindices.x]) * i.blendweight.x + mul(i.position, bones[i.blendindices.y]) * i.blendweight.y + mul(i.position, bones[i.blendindices.z]) * i.blendweight.z + mul(i.position, bones[i.blendindices.w]) * i.blendweight.w;
	float t1 = tex2Dlod(heightMap, float4(i.texcoord.xy * heightUv + time, 0, 0)).x;
	float t2 = t1 * heightScale + t0.y;
	float4 t3 = mul(float4(t0.x, t2, t0.zw), worldViewProjection);
	o.position = t3;
	o.texcoord = i.texcoord.xy;
	o.texcoord1 = normalize(mul(i.normal.xyz, (float3x3)bones[i.blendindices.x]) * i.blendweight.x + mul(i.normal.xyz, (float3x3)bones[i.blendindices.y]) * i.blendweight.y + mul(i.normal.xyz, (float3x3)bones[i.blendindices.z]) * i.blendweight.z + mul(i.normal.xyz, (float3x3)bones[i.blendindices.w]) * i.blendweight.w);
	o.fog = saturate(0.00999999978 * t3.w);

	return o;
}
