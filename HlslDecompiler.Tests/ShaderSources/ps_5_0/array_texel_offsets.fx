SamplerComparisonState cmp;
SamplerState ss;
Texture2DArray<float> sh;
Texture1DArray l1;
Texture2DArray l2;

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 t0 = l1.Sample(ss, i.texcoord.xy, 3);
	float4 t1 = l2.SampleLevel(ss, i.texcoord1.xyz, i.texcoord1.w, int2(-2, 4));
	float t2 = sh.SampleCmpLevelZero(cmp, i.texcoord.xyz, i.texcoord.w, int2(1, -1)).x;
	return t2 + t0 + t1 + l2.Load((int4)i.texcoord1, int2(1, 1));
}
