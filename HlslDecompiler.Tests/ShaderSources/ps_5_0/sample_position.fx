Texture2DMS<float4, 4> source;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	nointerpolation uint sv_sampleindex : SV_SampleIndex;
};

float4 main(PS_IN i) : SV_Target
{
	return float4(source.GetSamplePosition(i.sv_sampleindex), i.texcoord);
}
