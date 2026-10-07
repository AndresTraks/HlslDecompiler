sampler2D s0;
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float3 r2;
	float4 r9;
	float2 r10;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r2 = r8.xyz * 2 + -1;
	r9 = dot(i.texcoord1.xyz, r2.xyz);
	r2 = r8.xyz * 2 + -1;
	r10.x = dot(i.texcoord2.xyz, r2.xyz);
	r2 = r8.xyz * 2 + -1;
	r10.y = dot(i.texcoord3.xyz, r2.xyz);
	r11 = tex2D(s3, r10.xy);
	r0 = r9 * r11 + i.color;
	o = r0;

	return o;
}
