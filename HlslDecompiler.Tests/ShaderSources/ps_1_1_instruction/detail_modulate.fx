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
	float3 r2;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r9 = tex2D(s1, i.texcoord1.xy);
	r2 = r8.xyz * r9.xyz;
	r0.xyz = r2.xyz * 2;
	r0.w = r8.w * i.color.w;
	r0.xyz = r0.xyz * i.color.xyz;
	o = r0;

	return o;
}
