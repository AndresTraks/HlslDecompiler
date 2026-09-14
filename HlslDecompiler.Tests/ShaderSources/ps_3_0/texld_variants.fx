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
	return tex2Dbias(samp, float4(i.texcoord.xy, 0, bias)) * tex2Dgrad(samp, i.texcoord.xy, i.texcoord1.xy, i.texcoord1.zw) + tex2Dproj(samp, i.texcoord) - tex2Dlod(samp, float4(i.texcoord.xy, 0, lod));
}
