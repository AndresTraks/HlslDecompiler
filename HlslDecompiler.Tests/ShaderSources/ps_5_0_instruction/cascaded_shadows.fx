cbuffer Shadows : register(b0)
{
	float4x4 cascadeTransforms[4];
	float4 cascadeSplits;
	float3 lightDirection;
	float shadowBias;
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerComparisonState shadowSampler;
SamplerState linearSampler;
Texture2DArray<float> shadowMaps;
Texture2D albedo;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 world : WORLD;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
	float depth : DEPTH;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	r0.xy = int2(3, 0);
	while (true) {
		r0.z = ((uint)r0.y >= 4) ? -1 : 0;
		if (asint(r0.z) != 0) break;
		r0.z = dot(cascadeSplits, icb[r0.y]);
		r0.z = (i.depth < r0.z) ? -1 : 0;
		if (asint(r0.z) != 0) {
			r0.x = r0.y;
			break;
		}
		r0.y = r0.y + 1;
		r0.x = 3;
	}
	r0.y = (int)r0.x << 2;
	r1.xyz = i.world.xyz;
	r1.w = 1;
	r2.x = dot(r1, transpose(cascadeTransforms[r0.y / 4])[0]);
	r2.y = dot(r1, transpose(cascadeTransforms[r0.y / 4])[1]);
	r2.z = dot(r1, transpose(cascadeTransforms[r0.y / 4])[2]);
	r0.y = dot(r1, transpose(cascadeTransforms[r0.y / 4])[3]);
	r0.yzw = r2.xyz / r0.yyy;
	r1.xy = r0.yz * float2(0.5, -0.5) + float2(0.5, 0.5);
	r1.z = (float)(uint)r0.x;
	r0.x = r0.w + -(shadowBias);
	r0.x = shadowMaps.SampleCmpLevelZero(shadowSampler, r1.xyz, r0.x);
	r0.y = dot(i.normal.xyz, i.normal.xyz);
	r0.y = rsqrt(r0.y);
	r0.yzw = r0.yyy * i.normal.xyz;
	r0.y = saturate(dot(r0.yzw, -(lightDirection.xyz)));
	r1 = albedo.Sample(linearSampler, i.texcoord.xy);
	r0.x = r0.y * r0.x + 0.100000001;
	o = r0.x * r1;

	return o;
}
