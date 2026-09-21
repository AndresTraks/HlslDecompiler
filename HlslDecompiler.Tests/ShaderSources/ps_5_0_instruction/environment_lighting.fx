cbuffer Lighting : register(b0)
{
	float4x4 lightViewProjection[3];
	float3 cascadeSplits;
	float shadowBias;
	float3 lightColor;
	float exposure;
};

SamplerState trilinear;
SamplerComparisonState shadowSampler;
TextureCube environment;
Texture2DArray shadowMaps;
Texture2D brdfLut;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float3 position : POSITION;
	float texcoord1 : TEXCOORD1;
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int3 r1;
	float3 r2;
	float4 r3;
	float3 r4;
	r0.x = dot(i.texcoord.xyz, i.texcoord.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * i.texcoord.xyz;
	r0.w = dot(i.normal.xyz, i.normal.xyz);
	r0.w = rsqrt(r0.w);
	r1 = asint(r0.www * i.normal.xyz);
	r0.w = dot(-(r0.xyz), asfloat(r1.xyz));
	r0.w = r0.w + r0.w;
	r2 = asfloat(r1.xyz) * -(r0.www) + -(r0.xyz);
	r0.x = saturate(dot(asfloat(r1.xyz), r0.xyz));
	r1 = asint(environment.SampleLevel(trilinear, asfloat(r1.xyz), 6).xyz);
	r0.z = i.texcoord1 * 6;
	r2 = environment.SampleLevel(trilinear, r2.xyz, r0.zzz).xyz;
	r0.y = i.texcoord1;
	r0.xy = brdfLut.Sample(trilinear, r0.xy).xy;
	r0.x = r0.y + r0.x;
	r0.xyz = r2.xyz * r0.xxx + asfloat(r1.xyz);
	r0.xyz = r0.xyz * lightColor.xyz;
	r1.xy = (i.sv_position.ww < cascadeSplits.xy) ? -1 : 0;
	r0.w = (r1.y != 0) ? 1 : 2;
	r0.w = (r1.x != 0) ? 0 : r0.w;
	r1.x = (int)(int)r0.w << 2;
	r2.z = (float)(uint)r0.w;
	r3.xyz = i.position.xyz;
	r3.w = 1;
	r4.x = dot(r3, transpose(lightViewProjection[r1.x / 4])[0]);
	r4.y = dot(r3, transpose(lightViewProjection[r1.x / 4])[1]);
	r4.z = dot(r3, transpose(lightViewProjection[r1.x / 4])[2]);
	r0.w = dot(r3, transpose(lightViewProjection[r1.x / 4])[3]);
	r1 = asint(r4.xyz / r0.www);
	r2.xy = asfloat(r1.xy) * float2(0.5, -0.5) + float2(0.5, 0.5);
	r0.w = asfloat(r1.z) + -(shadowBias);
	r0.w = shadowMaps.SampleCmpLevelZero(shadowSampler, r2.xyz, r0.w);
	r0.xyz = r0.www * r0.xyz;
	r0.xyz = -(r0.xyz) * exposure;
	r0.xyz = r0.xyz * float3(1.44269502, 1.44269502, 1.44269502);
	r0.xyz = exp2(r0.xyz);
	r0.xyz = -(r0.xyz) + float3(1, 1, 1);
	r0.xyz = log2(r0.xyz);
	r0.xyz = r0.xyz * float3(0.454545468, 0.454545468, 0.454545468);
	o.xyz = exp2(r0.xyz);
	o.w = 1;

	return o;
}
