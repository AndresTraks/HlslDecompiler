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

	int4 r0;
	int4 r1;
	float2 r2;
	r0.x = asint(dot(transpose(lightViewProj)[3], i.texcoord));
	r1.x = asint(dot(i.texcoord, transpose(lightViewProj)[0]));
	r1.y = asint(dot(i.texcoord, transpose(lightViewProj)[1]));
	r1.z = asint(dot(i.texcoord, transpose(lightViewProj)[2]));
	r0.xyz = asint(asfloat(r1.xyz) / asfloat(r0.xxx));
	r1.yz = asint(asfloat(r0.xy) * float2(0.5, -0.5) + float2(0.5, 0.5));
	r0.x = asint(asfloat(r0.z) + -(bias.x));
	r2.y = asfloat(r1.z) + -(bias.w);
	r0.yz = int2(0, -1);
	while (true) {
		r0.w = (1 < r0.z) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = asint((float)r0.z);
		r2.x = asfloat(r0.w) * bias.z + asfloat(r1.y);
		r0.w = asint(shadowMap.SampleCmpLevelZero(shadowSamp, r2.xy, asfloat(r0.x)));
		r0.y = asint(asfloat(r0.w) + asfloat(r0.y));
		r0.z = r0.z + 1;
	}
	r0.z = r0.y;
	r0.w = -1;
	while (true) {
		r1.w = (1 < r0.w) ? -1 : 0;
		if (r1.w != 0) break;
		r1.w = asint((float)r0.w);
		r1.x = asint(asfloat(r1.w) * bias.z + asfloat(r1.y));
		r1.x = asint(shadowMap.SampleCmpLevelZero(shadowSamp, asfloat(r1.xz), asfloat(r0.x)));
		r0.z = asint(asfloat(r0.z) + asfloat(r1.x));
		r0.w = r0.w + 1;
	}
	r2.y = asfloat(r1.z) + bias.w;
	r0.y = r0.z;
	r0.w = -1;
	while (true) {
		r1.x = (1 < r0.w) ? -1 : 0;
		if (r1.x != 0) break;
		r1.x = asint((float)r0.w);
		r2.x = asfloat(r1.x) * bias.z + asfloat(r1.y);
		r1.x = asint(shadowMap.SampleCmpLevelZero(shadowSamp, r2.xy, asfloat(r0.x)));
		r0.y = asint(asfloat(r0.y) + asfloat(r1.x));
		r0.w = r0.w + 1;
	}
	r1 = asint(albedo.Sample(samp, i.texcoord1.xy));
	r0.x = asint(asfloat(r0.y) * 0.111111112);
	o = asfloat(r1) * asfloat(r0.x);

	return o;
}
