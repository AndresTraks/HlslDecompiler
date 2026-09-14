float bias : register(c1);
float lod;
sampler2D samp;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xyz = float3(1, 1, 0) * i.texcoord.xyx;
	r0.w = bias.x;
	r0 = tex2Dbias(samp, r0);
	r1 = tex2Dproj(samp, i.texcoord);
	r2.xy = i.texcoord.xy;
	r2 = tex2Dgrad(samp, r2.xy, i.texcoord1.xy, i.texcoord1.zw);
	r0 = r0 * r2 + r1;
	r1.xyz = float3(1, 1, 0) * i.texcoord.xyx;
	r1.w = lod.x;
	r1 = tex2Dlod(samp, r1);
	o = r0 + -r1;

	return o;
}
