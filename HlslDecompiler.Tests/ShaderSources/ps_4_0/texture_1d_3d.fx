SamplerState samp;
Texture1D gradient;
Texture3D volume;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = volume.SampleLevel(samp, texcoord.yzw, 2);
	float4 t1 = volume.Sample(samp, texcoord.xyz);
	float4 t2 = gradient.Sample(samp, texcoord.x);
	return t2 * t1 + t0;
}
