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

	float4 r0;
	float3 r1;
	r0 = int4(0, 0, 0, 0);
	while (true) {
		r1.x = (r0.w >= count) ? -1 : 0;
		if (asint(r1.x) != 0) break;
		r1 = icb0[r0.w].xyz * scale + icb1[r0.w].xyz;
		r0.xyz = r0.xyz + r1.xyz;
		r0.w = r0.w + 1;
	}
	o.xyz = r0.xyz * texcoord.xxx;
	o.w = 0;

	return o;
}
