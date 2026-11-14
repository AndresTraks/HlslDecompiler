float4 c;

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	r0 = t.Sample(s, texcoord.xy);
	o.x = dot(c.xy, r0.xy);
	o.y = dot(c.zw, r0.zw);
	o.z = dot(c.xz, r0.xz);
	o.w = dot(c.yw, r0.yw);

	return o;
}
