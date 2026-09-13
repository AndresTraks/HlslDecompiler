SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float r2;
	r0 = tex.Sample(samp, texcoord.xy);
	r1.xy = ddx(texcoord.xy);
	r1.zw = ddy(texcoord.xy);
	r2 = abs(r1.z) + abs(r1.x);
	o = r0 * r2.x + r1;

	return o;
}
