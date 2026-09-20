float4 probe;

SamplerState samp;
TextureCubeArray probes;
Texture2D albedoMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 normal : NORMAL;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.x = dot(i.normal.xyz, i.normal.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * i.normal.xyz;
	r0.w = dot(i.texcoord1.xyz, i.texcoord1.xyz);
	r0.w = rsqrt(r0.w);
	r1.xyz = r0.www * i.texcoord1.xyz;
	r0.w = dot(-(r1.xyz), r0.xyz);
	r0.w = r0.w + r0.w;
	r2.xyz = r0.xyz * -(r0.www) + -(r1.xyz);
	r0.x = saturate(dot(r0.xyz, r1.xyz));
	r0.x = -(r0.x) + 1;
	r2.w = probe.x;
	r1 = albedoMap.Sample(samp, i.texcoord.xy);
	r0.y = r1.w * probe.y;
	r0.yzw = probes.SampleLevel(samp, r2, r0.yyy).xyz;
	r0.yzw = -(r1.xyz) + r0.yzw;
	r1.w = r0.x * r0.x;
	r1.w = r1.w * r1.w;
	r0.x = r0.x * r1.w;
	r0.x = r0.x * 0.959999979 + 0.0399999991;
	o.xyz = r0.xxx * r0.yzw + r1.xyz;
	o.w = 1;

	return o;
}
