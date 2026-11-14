float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = t.Sample(s, texcoord.xy);
	o.x = dot(r0.xyz, c.xyz);
	r0 = r0 * c;
	o.yz = r0.zw + r0.yx;
	o.w = r0.w;

	return o;
}
