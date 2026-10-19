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
	int4 r1;
	int3 r2;
	r0.x = dot(i.texcoord, transpose(lightViewProj)[0]);
	r0.y = dot(i.texcoord, transpose(lightViewProj)[1]);
	r0.z = dot(i.texcoord, transpose(lightViewProj)[2]);
	r0.w = dot(i.texcoord, transpose(lightViewProj)[3]);
	r0.xyz = r0.xyz / r0.www;
	r0.xy = r0.xy * float2(0.5, -0.5) + float2(0.5, 0.5);
	r0.z = r0.z + -(bias.x);
	r1.yw = int2(-1082130432, 0);
	r0.w = 0;
	r2.x = -1;
	while (true) {
		r2.y = (1 < r2.x) ? -1 : 0;
		if (r2.y != 0) break;
		r1.x = asint((float)r2.x);
		r2.yz = asint(asfloat(r1.xy) * bias.zw + r0.xy);
		r1.x = asint(shadowMap.SampleCmpLevelZero(shadowSamp, asfloat(r2.yz), r0.z));
		r0.w = r0.w + asfloat(r1.x);
		r2.x = r2.x + 1;
	}
	r1.x = asint(r0.w);
	r1.y = -1;
	while (true) {
		r2.x = (1 < r1.y) ? -1 : 0;
		if (r2.x != 0) break;
		r1.z = asint((float)r1.y);
		r2.xy = asint(asfloat(r1.zw) * bias.zw + r0.xy);
		r1.z = asint(shadowMap.SampleCmpLevelZero(shadowSamp, asfloat(r2.xy), r0.z));
		r1.x = asint(asfloat(r1.z) + asfloat(r1.x));
		r1.y = r1.y + 1;
	}
	r2.y = 1065353216;
	r0.w = asfloat(r1.x);
	r1.y = -1;
	while (true) {
		r1.z = (1 < r1.y) ? -1 : 0;
		if (r1.z != 0) break;
		r2.x = asint((float)r1.y);
		r1.zw = asint(asfloat(r2.xy) * bias.zw + r0.xy);
		r1.z = asint(shadowMap.SampleCmpLevelZero(shadowSamp, asfloat(r1.zw), r0.z));
		r0.w = r0.w + asfloat(r1.z);
		r1.y = r1.y + 1;
	}
	r1 = asint(albedo.Sample(samp, i.texcoord1.xy));
	r0.x = r0.w * 0.111111112;
	o = r0.x * asfloat(r1);

	return o;
}
