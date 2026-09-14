SamplerState samp;
Texture1D gradient;
Texture3D volume;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = gradient.Sample(samp, texcoord.x);
	r1 = volume.Sample(samp, texcoord.xyz);
	r2 = volume.SampleLevel(samp, texcoord.yzw, 2);
	o = r0 * r1 + r2;

	return o;
}
