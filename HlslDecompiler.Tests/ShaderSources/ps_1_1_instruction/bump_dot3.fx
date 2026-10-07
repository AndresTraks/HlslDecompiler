sampler2D s0;
sampler2D s1;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float4 r9;
	float4 r2;
	float3 r3;
	float4 r1;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r9 = tex2D(s1, i.texcoord1.xy);
	r2.xyz = r8.xyz * 2 + -1;
	r3 = i.color.xyz * 2 + -1;
	r1 = saturate(dot(r2.xyz, r3.xyz));
	r1 = r1 + float4(0.2, 0.2, 0.25, 1);
	r0.xyz = r9.xyz * r1.xyz;
	r2.w = -r9.w + 1;
	r0.w = r2.w;
	o = r0;

	return o;
}
