SamplerState samp;
Texture2D tex;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0.xy = ddx(texcoord.xy);
	r0.zw = ddy(texcoord.xy);
	o = tex.SampleGrad(samp, texcoord.xyxx, r0.xyxx, r0.zwzz);

	return o;
}
