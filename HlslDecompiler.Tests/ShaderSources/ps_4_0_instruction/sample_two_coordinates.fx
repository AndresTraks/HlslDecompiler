SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0.xy = texcoord.xy + texcoord.xy;
	r0 = tex.Sample(samp, r0.xy);
	o.yz = r0.yz;
	r0 = tex.Sample(samp, texcoord.xy);
	o.x = r0.y;
	o.w = 1;

	return o;
}
