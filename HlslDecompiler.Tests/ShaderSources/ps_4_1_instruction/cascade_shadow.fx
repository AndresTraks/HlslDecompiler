float4x4 cascadeTransform[4];
float4 cascadeSplit;
float4 shadowParameters;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerComparisonState shadowSampler;
Texture2DArray cascades;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float3 r3;
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = (r0.y >= 4) ? -1 : 0;
		if (asint(r0.z) != 0) break;
		r0.z = dot(cascadeSplit, icb[r0.y]);
		r0.z = (r0.z < i.texcoord1) ? -1 : 0;
		r0.y = r0.y + 1;
		r0.x = (asint(r0.z) != 0) ? r0.y : r0.x;
	}
	r0.x = min(r0.x, 3);
	r0.y = (int)r0.x << 2;
	r1.xyz = i.texcoord.xyz;
	r1.w = 1;
	r2.x = dot(r1, transpose(cascadeTransform[r0.y / 4])[0]);
	r2.y = dot(r1, transpose(cascadeTransform[r0.y / 4])[1]);
	r0.z = dot(r1, transpose(cascadeTransform[r0.y / 4])[2]);
	r0.y = dot(r1, transpose(cascadeTransform[r0.y / 4])[3]);
	r1.xy = r2.xy / r0.yy;
	r2.y = r1.y * -0.5;
	r3.xy = r1.xy * float2(0.5, -0.5) + float2(0.5, 0.5);
	r0.w = saturate(i.normal.y);
	r1.y = -(r0.w) + 1;
	r1.y = shadowParameters.x * r1.y + shadowParameters.y;
	r3.z = (float)(int)r0.x;
	r0.x = r0.z / r0.y;
	r0.x = -(r1.y) + r0.x;
	r0.y = cascades.SampleCmpLevelZero(shadowSampler, r3.xyz, r0.x);
	r2.z = r1.x * 0.5 + 0.5;
	r1.xz = shadowParameters.zz;
	r1.yw = float2(0.5, 0);
	r2.xy = r1.xy + r2.zy;
	r2.z = r3.z;
	r0.z = cascades.SampleCmpLevelZero(shadowSampler, r2.xyz, r0.x);
	r0.y = r0.z + r0.y;
	r2.xy = -(r1.zw) + r3.xy;
	r0.x = cascades.SampleCmpLevelZero(shadowSampler, r2.xyz, r0.x);
	r0.x = r0.x + r0.y;
	r0.x = r0.w * r0.x;
	o = r0.x * float4(0.333333343, 0.333333343, 0.333333343, 0.333333343);

	return o;
}
