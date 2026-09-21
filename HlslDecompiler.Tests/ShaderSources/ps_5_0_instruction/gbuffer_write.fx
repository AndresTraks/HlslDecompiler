cbuffer Material : register(b0)
{
	float4 baseColor;
	float roughness;
	float metallic;
	uint mode;
	float alphaCutoff;
};

SamplerState anisoSampler;
Texture2D albedoMap;
Texture2D normalMap;
Texture2D maskMap;

struct PS_IN
{
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float2 sv_target2 : SV_Target2;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	int4 r0;
	float3 r1;
	float4 r2;
	float3 r3;
	float2 r4;
	r0 = asint(albedoMap.Sample(anisoSampler, i.texcoord.xy));
	r1 = asfloat(r0.xyz) * baseColor.xyz;
	r2 = maskMap.Sample(anisoSampler, i.texcoord.xy);
	switch (mode) {
		case 0:
		r0.w = asint(asfloat(r0.w) * baseColor.w + -(alphaCutoff));
		r0.w = (asfloat(r0.w) < 0) ? -1 : 0;
		if (r0.w != 0) discard;
		break;
		case 1:
		r1 = r1.xyz * r2.xxx;
		break;
		case 2:
		r0.xyz = asint(-(asfloat(r0.xyz)) * baseColor.xyz + r2.xyz);
		r1 = r2.www * asfloat(r0.xyz) + r1.xyz;
		break;
		default:
		break;
	}
	o.sv_target.xyz = r1.xyz;
	r0.x = asint(dot(i.normal.xyz, i.normal.xyz));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * i.normal.xyz);
	r0.w = asint(dot(i.tangent.xyz, asfloat(r0.xyz)));
	r1 = -(asfloat(r0.xyz)) * asfloat(r0.www) + i.tangent.xyz;
	r0.w = asint(dot(r1.xyz, r1.xyz));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r1 = asfloat(r0.www) * r1.xyz;
	r3 = asfloat(r0.zxy) * r1.yzx;
	r3 = asfloat(r0.yzx) * r1.zxy + -(r3.xyz);
	r3 = r3.xyz * i.tangent.www;
	r4 = normalMap.Sample(anisoSampler, i.texcoord.xy).xy;
	r4 = r4.xy * float2(2, 2) + float2(-1, -1);
	r0.w = asint(dot(r4.xy, r4.xy));
	r0.w = asint(-(asfloat(r0.w)) + 1);
	r0.w = asint(max(asfloat(r0.w), 0));
	r0.w = asint(sqrt(asfloat(r0.w)));
	r3 = r3.xyz * r4.yyy;
	r1 = r4.xxx * r1.xyz + r3.xyz;
	r0.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz) + r1.xyz);
	r0.w = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r0.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz));
	r0.w = asint(dot(i.texcoord1.xyz, i.texcoord1.xyz));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r1 = asfloat(r0.www) * i.texcoord1.xyz;
	r0.w = asint(saturate(dot(asfloat(r0.xyz), r1.xyz)));
	r0.w = asint(-(asfloat(r0.w)) + 1);
	r1.x = asfloat(r0.w) * asfloat(r0.w);
	r1.x = r1.x * r1.x;
	o.sv_target.w = asfloat(r0.w) * r1.x;
	o.sv_target1.xyz = asfloat(r0.xyz) * float3(0.5, 0.5, 0.5) + float3(0.5, 0.5, 0.5);
	r0.x = (mode == 2) ? -1 : 0;
	o.sv_target1.w = (r0.x != 0) ? r2.w : 1;
	o.sv_target2 = r2.yz * float2(roughness, metallic);

	return o;
}
