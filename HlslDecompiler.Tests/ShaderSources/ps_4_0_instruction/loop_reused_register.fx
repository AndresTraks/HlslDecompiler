cbuffer cb : register(b0)
{
	float4x4 lightViewProj;
	float4 bias;
};

SamplerComparisonState shadowSamp;
SamplerState samp;
Texture2D shadowMap;
Texture2D albedo;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float4 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float2 r2;
	r0.x = dot(transpose(lightViewProj)[3], i.texcoord);
	r1.x = dot(i.texcoord, transpose(lightViewProj)[0]);
	r1.y = dot(i.texcoord, transpose(lightViewProj)[1]);
	r1.z = dot(i.texcoord, transpose(lightViewProj)[2]);
	r0.xyz = r1.xyz / r0.xxx;
	r1.yz = r0.xy * float2(0.5, -0.5) + float2(0.5, 0.5);
	r0.x = r0.z + -(bias.x);
	r2.y = r1.z + -(bias.w);
	r0.yz = int2(0, -1);
	while (true) {
		r0.w = (1 < r0.z) ? -1 : 0;
		if (asint(r0.w) != 0) break;
		r0.w = (float)(int)r0.z;
		r2.x = r0.w * bias.z + r1.y;
		r0.w = shadowMap.SampleCmpLevelZero(shadowSamp, r2.xy, r0.x);
		r0.y = r0.w + r0.y;
		r0.z = r0.z + 1;
	}
	r0.z = r0.y;
	r0.w = -1;
	while (true) {
		r1.w = (1 < r0.w) ? -1 : 0;
		if (asint(r1.w) != 0) break;
		r1.w = (float)(int)r0.w;
		r1.x = r1.w * bias.z + r1.y;
		r1.x = shadowMap.SampleCmpLevelZero(shadowSamp, r1.xz, r0.x);
		r0.z = r0.z + r1.x;
		r0.w = r0.w + 1;
	}
	r2.y = r1.z + bias.w;
	r0.y = r0.z;
	r0.w = -1;
	while (true) {
		r1.x = (1 < r0.w) ? -1 : 0;
		if (asint(r1.x) != 0) break;
		r1.x = (float)(int)r0.w;
		r2.x = r1.x * bias.z + r1.y;
		r1.x = shadowMap.SampleCmpLevelZero(shadowSamp, r2.xy, r0.x);
		r0.y = r0.y + r1.x;
		r0.w = r0.w + 1;
	}
	r1 = albedo.Sample(samp, i.texcoord1.xy);
	r0.x = r0.y * 0.111111112;
	o = r1 * r0.x;

	return o;
}
