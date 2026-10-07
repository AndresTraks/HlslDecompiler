sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float4 color1 : COLOR1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float4 r9;
	float4 r2;
	float4 r3;
	float4 r0;
	float4 r1;
	float3 r7;
	r8 = tex2D(s0, i.texcoord.xy);
	r9.xyz = saturate(i.texcoord1.xyz);
	r9.w = 1;
	clip(i.texcoord2);
	r2 = r9 + -0.5;
	r3 = r8 + -r2;
	r0 = r3 * 0.5;
	r2 = r9 * -2 + 1;
	r3 = r0 * r2 + i.color1;
	r1 = saturate(r3 * 4);
	r7 = lerp(r8.xyz, r1.xyz, float3(0.5, 0.25, 0.75));
	r0.w = r1.w + r0.z;
	r0.xyz = r7.xyz;
	r2 = dot(r0, float4(0.3, -0.7, 0.4, 0.9));
	r1 = r2 * 0.25;
	r2 = r1 + -0.5;
	r3 = -r8 + 0.5;
	r0.xyz = (r2.xyz >= 0) ? r0.xyz : r3.xyz;
	r2 = -r9 + 1;
	r3 = -r0.w + 0.5;
	r0 = (r3 >= 0) ? r2 : r0;
	o = r0;

	return o;
}
