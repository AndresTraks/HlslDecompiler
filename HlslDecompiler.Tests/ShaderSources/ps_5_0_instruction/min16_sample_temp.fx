min16float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = t.Sample(s, texcoord.xy);
	r0 = r0 * c;
	r0 = r0.yzwx + r0;
	o = r0;

	return o;
}
