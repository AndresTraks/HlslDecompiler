cbuffer c : register(b0)
{
	uint mode;
	float4 tint;
};

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	int2 r1;
	r0 = t.Sample(s, texcoord.xy);
	r1.x = (mode == 2) ? -1 : 0;
	if (r1.x != 0) {
		r1.x = asint(dot(r0.xyz, float3(0.298999995, 0.587000012, 0.114000000)));
		r1.y = asint(r0.w);
		o = asfloat(r1.xxxy);
		return o;
	}
	o = r0 * tint;

	return o;
}
