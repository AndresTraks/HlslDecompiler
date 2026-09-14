SamplerState samp;
Texture1D gradient;
Texture3D volume;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	return gradient.Sample(samp, texcoord.x) * volume.Sample(samp, texcoord.xyz) + volume.SampleLevel(samp, texcoord.yzw, 2);
}
