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
	r0 = t.Sample(s, texcoord.xy).xwyz;
	switch (mode) {
		case 2:
		r0.x = dot(r0.xzw, float3(0.298999995, 0.587000012, 0.114000000));
		o = r0.xxxy;
		return o;
		default:
		o = r0.xzwy * tint;
		return o;
	}

	return o;
}
