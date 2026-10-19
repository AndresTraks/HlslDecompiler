cbuffer Params : register(b0)
{
	uint count;
	float scale;
};

static const float4 icb0[3] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
};

static const float4 icb1[3] =
{
	float4(0.25, 0.5, 0.25, 0),
	float4(0.5, 0.25, 0.25, 0),
	float4(0.25, 0.25, 0.5, 0),
};

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	int4 r0;
	int3 r1;
	r0 = int4(0, 0, 0, 0);
	while (true) {
		r1.x = ((uint)r0.w >= count) ? -1 : 0;
		if (r1.x != 0) break;
		r1 = asint(icb0[r0.w].xyz * scale + icb1[r0.w].xyz);
		r0.xyz = asint(asfloat(r0.xyz) + asfloat(r1.xyz));
		r0.w = r0.w + 1;
	}
	o.xyz = asfloat(r0.xyz) * texcoord.xxx;
	o.w = 0;

	return o;
}
