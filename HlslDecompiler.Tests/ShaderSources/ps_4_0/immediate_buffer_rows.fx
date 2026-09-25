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
	float3 t0 = 0;
	for (uint t1 = 0; t1 < count; t1 = t1 + 1) {
		t0 = icb0[t1].xyz * scale + icb1[t1].xyz + t0;
	}
	return float4(t0 * texcoord.xxx, 0);
}
