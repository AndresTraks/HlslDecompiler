SamplerState samp;
Texture1D gradient;
Texture3D volume;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = gradient.Sample(samp, texcoord.x);
	float4 t1 = volume.Sample(samp, texcoord.xyz);
	float4 t2 = volume.SampleLevel(samp, texcoord.yzw, 2);
	return t0 * t1 + t2;
}
