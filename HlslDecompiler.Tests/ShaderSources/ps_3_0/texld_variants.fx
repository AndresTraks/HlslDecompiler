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
	float4 t0 = tex2Dlod(samp, float4(i.texcoord.xy, 0, lod));
	float4 t1 = tex2Dproj(samp, i.texcoord);
	float4 t2 = tex2Dgrad(samp, i.texcoord.xy, i.texcoord1.xy, i.texcoord1.zw);
	float4 t3 = tex2Dbias(samp, float4(i.texcoord.xy, 0, bias));
	return t3 * t2 + t1 - t0;
}
