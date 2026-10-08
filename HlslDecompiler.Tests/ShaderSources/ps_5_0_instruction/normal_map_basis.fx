float3 lightDirection;

SamplerState samp;
Texture2D normalMap;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float3 r1;
	float3 r2;
	float3 r3;
	r0.x = dot(i.normal.xyz, i.normal.xyz);
	r0.x = rsqrt(r0.x);
	r0.xyz = r0.xxx * i.normal.xyz;
	r0.w = dot(i.tangent.xyz, r0.xyz);
	r1 = -(r0.yzx) * r0.www + i.tangent.yzx;
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1 = r0.www * r1.xyz;
	r2 = r0.zxy * r1.xyz;
	r2 = r0.yzx * r1.yzx + -(r2.xyz);
	r2 = r2.xyz * i.tangent.www;
	r3 = normalMap.Sample(samp, i.texcoord.xy).xyz;
	r3 = r3.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r2 = r2.xyz * r3.yyy;
	r1 = r1.zxy * r3.xxx + r2.xyz;
	r0.xyz = r0.xyz * r3.zzz + r1.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	o.xyz = r0.xyz * float3(0.5, 0.5, 0.5) + float3(0.5, 0.5, 0.5);
	o.w = saturate(dot(lightDirection.xyz, r0.xyz));

	return o;
}
