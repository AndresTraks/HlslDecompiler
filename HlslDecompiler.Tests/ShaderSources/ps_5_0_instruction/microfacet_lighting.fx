cbuffer Params : register(b0)
{
	float3 lightDirection;
	float lightIntensity;
	float3 eye;
	float metallic;
};

SamplerState linearSampler;
Texture2D albedoMap;
Texture2D roughnessMap;
TextureCube environment;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 normal : NORMAL;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float3 r4;
	float3 r5;
	r0.xyz = -(i.texcoord.xyz) + eye.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = r0.xyz * r0.www + -(lightDirection.xyz);
	r0.xyz = r0.www * r0.xyz;
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = r0.www * r1.xyz;
	r0.w = dot(i.normal.xyz, i.normal.xyz);
	r0.w = rsqrt(r0.w);
	r2.xyz = r0.www * i.normal.xyz;
	r0.w = saturate(dot(r2.xyz, r1.xyz));
	r1.x = saturate(dot(r0.xyz, r1.xyz));
	r1.x = -(r1.x) + 1;
	r0.w = r0.w * r0.w;
	r1.y = roughnessMap.Sample(linearSampler, i.texcoord1.xy).x;
	r1.z = r1.y * r1.y;
	r1.z = max(r1.z, 0.00200000009);
	r1.w = r1.z * r1.z + -1;
	r0.w = r0.w * r1.w + 1;
	r0.w = r0.w * r0.w;
	r0.w = r0.w * 3.14159274;
	r1.w = r1.z * r1.z;
	r0.w = r1.w / r0.w;
	r1.yw = r1.yz * float2(8, 0.5);
	r1.z = -(r1.z) * 0.5 + 1;
	r2.w = saturate(dot(r2.xyz, r0.xyz));
	r2.w = r2.w * r1.z + r1.w;
	r3.x = saturate(dot(r2.xyz, -(lightDirection.xyz)));
	r1.z = r3.x * r1.z + r1.w;
	r1.z = r2.w * r1.z;
	r1.z = 1 / r1.z;
	r0.w = r0.w * r1.z;
	r1.z = r1.x * r1.x;
	r1.z = r1.z * r1.z;
	r1.x = r1.z * r1.x;
	r3.yzw = albedoMap.Sample(linearSampler, i.texcoord1.xy).xyz;
	r4 = r3.yzw + float3(-0.0399999991, -0.0399999991, -0.0399999991);
	r4 = metallic * r4.xyz + float3(0.0399999991, 0.0399999991, 0.0399999991);
	r5 = -(r4.xyz) + float3(1, 1, 1);
	r1.xzw = r5.xyz * r1.xxx + r4.xyz;
	r5 = r0.www * r1.xzw;
	r1.xzw = -(r1.xzw) + float3(1, 1, 1);
	r5 = r5.xyz * float3(0.25, 0.25, 0.25);
	r0.w = -(metallic) + 1;
	r3.yzw = r0.www * r3.yzw;
	r1.xzw = r3.yzw * r1.xzw + r5.xyz;
	r1.xzw = r3.xxx * r1.xzw;
	r0.w = dot(-(r0.xyz), r2.xyz);
	r0.w = r0.w + r0.w;
	r0.xyz = r2.xyz * -(r0.www) + -(r0.xyz);
	r0.xyz = environment.SampleLevel(linearSampler, r0.xyz, r1.yyy).xyz;
	r0.xyz = r4.xyz * r0.xyz;
	o.xyz = r1.xzw * lightIntensity + r0.xyz;
	o.w = 1;

	return o;
}
