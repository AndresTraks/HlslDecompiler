float time;
float3 eyePos;
float4 tint;

SamplerState samp;
Texture2D normal0;
Texture2D normal1;
Texture2D reflection;
Texture2D refraction;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xyz = -(i.texcoord.xyz) + eyePos.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = 1 / sqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r1.xw = time * float2(0.0199999996, 0.0130000003);
	r1.yz = float2(0, 0);
	r1.xy = r1.xy + i.texcoord2.xy;
	r1.zw = i.texcoord2.xy * float2(1.70000005, 1.70000005) + -(r1.zw);
	r2 = normal1.Sample(samp, r1.zw);
	r1 = normal0.Sample(samp, r1.xy);
	r1.xyz = r1.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r1.xyz = r2.xyz * float3(2, 2, 2) + r1.xyz;
	r1.xyz = r1.xyz + float3(-1, -1, -1);
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = 1 / sqrt(r0.w);
	r1.xyz = r0.www * r1.xyz;
	r0.x = saturate(dot(r0.xyz, r1.xyz));
	r0.x = -(r0.x) + 1;
	r0.y = r0.x * r0.x;
	r0.y = r0.y * r0.y;
	r0.x = r0.y * r0.x;
	r0.x = r0.x * 0.899999976 + 0.100000001;
	r0.yz = i.texcoord1.xy / i.texcoord1.ww;
	r0.yz = r0.yz * float2(0.5, 0.5) + float2(0.5, 0.5);
	r1.yw = r1.xz * float2(0.0299999993, 0.0299999993) + r0.yz;
	r0.yz = -(r1.xz) * float2(0.0199999996, 0.0199999996) + r0.yz;
	r2 = refraction.Sample(samp, r0.yz);
	r1 = reflection.Sample(samp, r1.yw);
	r0.yzw = -(r2.xyz) + r1.xyz;
	r0.xyz = r0.xxx * r0.yzw + r2.xyz;
	o.xyz = r0.xyz * tint.xyz;
	o.w = 1;

	return o;
}
