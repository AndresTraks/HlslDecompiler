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
	float4 o;

	float4 r0;
	float4 r1;
	r0.x = sh.SampleCmpLevelZero(cmp, i.texcoord.xyz, i.texcoord.w, int2(1, -1));
	r1 = l1.Sample(ss, i.texcoord.xy, 3);
	r0 = r0.x + r1;
	r1 = l2.SampleLevel(ss, i.texcoord1.xyz, i.texcoord1.w, int2(-2, 4));
	r0 = r0 + r1;
	r1 = (int4)i.texcoord1;
	r1 = l2.Load(r1, int2(1, 1));
	o = r0 + r1;

	return o;
}
