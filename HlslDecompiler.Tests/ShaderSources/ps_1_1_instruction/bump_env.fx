float4 bumpEnvMat1 : register(c9);
float4 bumpEnvMat2 : register(c10);
float2 bumpEnvLum2 : register(c18);
sampler2D s0;
sampler2D s1;
sampler2D s2;
sampler2D s3;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float2 r2;
	float4 r9;
	float4 r10;
	float r3;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r2 = r8.xx * bumpEnvMat1.xy;
	r2 = r8.yy * bumpEnvMat1.zw + r2.xy;
	r2 = r2.xy + i.texcoord1.xy;
	r9 = tex2D(s1, r2.xy);
	r2 = r8.xx * bumpEnvMat2.xy;
	r2 = r8.yy * bumpEnvMat2.zw + r2.xy;
	r2 = r2.xy + i.texcoord2.xy;
	r10 = tex2D(s2, r2.xy);
	r3 = saturate(r8.z * bumpEnvLum2.x + bumpEnvLum2.y);
	r10.xyz = r10.xyz * r3.xxx;
	r2 = r8.yz;
	r11 = tex2D(s3, r2.xy);
	r0 = r9 * r10;
	r0 = r11 * i.color + r0;
	o = r0;

	return o;
}
