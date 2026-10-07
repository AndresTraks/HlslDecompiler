sampler2D s0;
sampler2D s1;
sampler2D s2;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r6;
	float4 r2;
	float3 r7;
	r0 = tex2D(s0, i.texcoord.xy);
	r1.xyz = i.texcoord1.xyz;
	r6.xy = r0.xy * 2 + -1;
	r1.xy = r6.xy * float2(0.08, -0.06) + r1.xy;
	r1 = tex2D(s1, r1.xy);
	r6.w = 1 / i.texcoord2.w;
	r6.xy = i.texcoord2.xy * r6.ww;
	r2 = tex2D(s2, r6.xy);
	r0.xyz = r1.xyz * i.color.xyz;
	r6.w = r2.w * 2;
	r0.w = r6.w * i.color.w;
	r0.xyz = lerp(float3(0.9, 0.8, 0.7), r0.xyz, r2.xyz);
	r6.xyz = -r1.xyz + 1;
	r7 = -r2.xyz + 0.5;
	r0.xyz = (r7.xyz >= 0) ? r6.xyz : r0.xyz;
	o = r0;

	return o;
}
