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

	float4 r0;
	float4 r1;
	int4 a0;
	float4 r2;
	float4 r3;
	r0.xy = heightUv.xy;
	r0.xy = i.texcoord.xy * r0.xy + time.xx;
	r0.zw = 0;
	r0 = tex2Dlod(heightMap, r0);
	r1 = 4 * i.blendindices;
	a0 = r1.yxzw;
	r2.x = dot(i.position, transpose(bones[a0.x / 4])[0]);
	r2.y = dot(i.position, transpose(bones[a0.x / 4])[1]);
	r2.z = dot(i.position, transpose(bones[a0.x / 4])[2]);
	r2.w = dot(i.position, transpose(bones[a0.x / 4])[3]);
	r2 = r2 * i.blendweight.y;
	r3.x = dot(i.position, transpose(bones[a0.y / 4])[0]);
	r3.y = dot(i.position, transpose(bones[a0.y / 4])[1]);
	r3.z = dot(i.position, transpose(bones[a0.y / 4])[2]);
	r3.w = dot(i.position, transpose(bones[a0.y / 4])[3]);
	r2 = r3 * i.blendweight.x + r2;
	r1.x = dot(i.position, transpose(bones[a0.z / 4])[0]);
	r1.y = dot(i.position, transpose(bones[a0.z / 4])[1]);
	r1.z = dot(i.position, transpose(bones[a0.z / 4])[2]);
	r1.w = dot(i.position, transpose(bones[a0.z / 4])[3]);
	r1 = r1 * i.blendweight.z + r2;
	r2.x = dot(i.position, transpose(bones[a0.w / 4])[0]);
	r2.y = dot(i.position, transpose(bones[a0.w / 4])[1]);
	r2.z = dot(i.position, transpose(bones[a0.w / 4])[2]);
	r2.w = dot(i.position, transpose(bones[a0.w / 4])[3]);
	r1 = r2 * i.blendweight.w + r1;
	r1.y = r0.x * heightScale.x + r1.y;
	o.position.x = dot(r1, transpose(worldViewProjection)[0]);
	o.position.y = dot(r1, transpose(worldViewProjection)[1]);
	o.position.z = dot(r1, transpose(worldViewProjection)[2]);
	r0.x = dot(r1, transpose(worldViewProjection)[3]);
	r1.x = dot(i.normal.xyz, transpose(bones[a0.x / 4])[0].xyz);
	r1.y = dot(i.normal.xyz, transpose(bones[a0.x / 4])[1].xyz);
	r1.z = dot(i.normal.xyz, transpose(bones[a0.x / 4])[2].xyz);
	r0.yzw = r1.xyz * i.blendweight.yyy;
	r1.x = dot(i.normal.xyz, transpose(bones[a0.y / 4])[0].xyz);
	r1.y = dot(i.normal.xyz, transpose(bones[a0.y / 4])[1].xyz);
	r1.z = dot(i.normal.xyz, transpose(bones[a0.y / 4])[2].xyz);
	r0.yzw = r1.xyz * i.blendweight.xxx + r0.yzw;
	r1.x = dot(i.normal.xyz, transpose(bones[a0.z / 4])[0].xyz);
	r1.y = dot(i.normal.xyz, transpose(bones[a0.z / 4])[1].xyz);
	r1.z = dot(i.normal.xyz, transpose(bones[a0.z / 4])[2].xyz);
	r0.yzw = r1.xyz * i.blendweight.zzz + r0.yzw;
	r1.x = dot(i.normal.xyz, transpose(bones[a0.w / 4])[0].xyz);
	r1.y = dot(i.normal.xyz, transpose(bones[a0.w / 4])[1].xyz);
	r1.z = dot(i.normal.xyz, transpose(bones[a0.w / 4])[2].xyz);
	r0.yzw = r1.xyz * i.blendweight.www + r0.yzw;
	r1.x = dot(r0.yzw, r0.yzw);
	r1.x = rsqrt(r1.x);
	o.texcoord1 = r0.yzw * r1.xxx;
	o.fog = saturate(r0.x * 0.01);
	o.position.w = r0.x;
	o.texcoord = i.texcoord.xy;

	return o;
}
