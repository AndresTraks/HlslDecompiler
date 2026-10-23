cbuffer C : register(b0)
{
	float4x4 cas[4];
	float4 splitFar;
	float bias;
	float normalOffset;
	float blendBand;
	float2 poisson[4];
	float texel;
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

SamplerComparisonState cmp;
Texture2DArray<float> sm;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 wpos : WPOS;
	float viewz : VIEWZ;
	float3 normal : NORMAL;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	int3 r2;
	float3 r3;
	float3 r4;
	r0.xyz = i.normal.xyz * normalOffset + i.wpos.xyz;
	r1.xyz = (i.viewz < splitFar.zyx) ? -1 : 0;
	r1.x = (r1.x != 0) ? 2 : 3;
	r1.x = (r1.y != 0) ? 1 : r1.x;
	r1.x = (r1.z != 0) ? 0 : r1.x;
	r1.y = r1.x << 2;
	r0.w = 1;
	r2.x = asint(dot(r0, transpose(cas[r1.y / 4])[0]));
	r2.y = asint(dot(r0, transpose(cas[r1.y / 4])[1]));
	r1.z = asint(dot(r0, transpose(cas[r1.y / 4])[2]));
	r3.xy = poisson[0].xy * texel + asfloat(r2.xy);
	r3.z = (float)r1.x;
	r1.z = asint(asfloat(r1.z) + -(bias));
	r1.w = asint(sm.SampleCmpLevelZero(cmp, r3.xyz, asfloat(r1.z)));
	r3.xy = poisson[1].xy * texel + asfloat(r2.xy);
	r2.z = asint(sm.SampleCmpLevelZero(cmp, r3.xyz, asfloat(r1.z)));
	r1.w = asint(asfloat(r1.w) + asfloat(r2.z));
	r3.xy = poisson[2].xy * texel + asfloat(r2.xy);
	r2.z = asint(sm.SampleCmpLevelZero(cmp, r3.xyz, asfloat(r1.z)));
	r1.w = asint(asfloat(r1.w) + asfloat(r2.z));
	r3.xy = poisson[3].xy * texel + asfloat(r2.xy);
	r1.z = asint(sm.SampleCmpLevelZero(cmp, r3.xyz, asfloat(r1.z)));
	r1.z = asint(asfloat(r1.z) + asfloat(r1.w));
	r1.w = asint(asfloat(r1.z) * 0.25);
	r2.x = asint(dot(splitFar, icb[r1.x]));
	r2.x = asint(asfloat(r2.x) + -(i.viewz));
	r2.x = asint(asfloat(r2.x) / blendBand);
	r2.y = (asfloat(r2.x) < 1) ? -1 : 0;
	r2.z = (r1.x < 3) ? -1 : 0;
	r2.y = r2.z & r2.y;
	if (r2.y != 0) {
		r1.x = r1.x + 1;
		r3.x = dot(r0, transpose(cas[r1.y / 4 + 1])[0]);
		r3.y = dot(r0, transpose(cas[r1.y / 4 + 1])[1]);
		r0.x = dot(r0, transpose(cas[r1.y / 4 + 1])[2]);
		r4.xy = poisson[0].xy * texel + r3.xy;
		r4.z = (float)r1.x;
		r0.x = r0.x + -(bias);
		r0.y = sm.SampleCmpLevelZero(cmp, r4.xyz, r0.x);
		r4.xy = poisson[1].xy * texel + r3.xy;
		r0.z = sm.SampleCmpLevelZero(cmp, r4.xyz, r0.x);
		r0.y = r0.z + r0.y;
		r4.xy = poisson[2].xy * texel + r3.xy;
		r0.z = sm.SampleCmpLevelZero(cmp, r4.xyz, r0.x);
		r0.y = r0.z + r0.y;
		r4.xy = poisson[3].xy * texel + r3.xy;
		r0.x = sm.SampleCmpLevelZero(cmp, r4.xyz, r0.x);
		r0.x = r0.x + r0.y;
		r0.x = r0.x * 0.25;
		r0.y = asfloat(r1.z) * 0.25 + -(r0.x);
		r1.w = asint(asfloat(r2.x) * r0.y + r0.x);
	}
	o.xyz = asfloat(r1.www);
	o.w = 1;

	return o;
}
