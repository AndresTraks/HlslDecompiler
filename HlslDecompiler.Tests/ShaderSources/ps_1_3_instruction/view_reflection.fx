sampler2D s0;
sampler2D s3 : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
	float4 texcoord2 : TEXCOORD2;
	float4 texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float3 r2;
	float3 r9;
	float3 r3;
	float4 r4;
	float3 r5;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r2 = r8.xyz * 2 + -1;
	r9.x = dot(i.texcoord1.xyz, r2.xyz);
	r2 = r8.xyz * 2 + -1;
	r9.y = dot(i.texcoord2.xyz, r2.xyz);
	r2 = r8.xyz * 2 + -1;
	r9.z = dot(i.texcoord3.xyz, r2.xyz);
	r3.x = i.texcoord1.w;
	r3.y = i.texcoord2.w;
	r3.z = i.texcoord3.w;
	r4.x = dot(r9.xyz, r3.xyz);
	r4.y = dot(r9.xyz, r9.xyz);
	r4.z = 1 / r4.y;
	r4.w = r4.x * r4.z;
	r4.w = r4.w + r4.w;
	r5 = r9.xyz * r4.www + -r3.xyz;
	r11 = tex2D(s3, r5.xy);
	r0 = r11;
	o = r0;

	return o;
}
