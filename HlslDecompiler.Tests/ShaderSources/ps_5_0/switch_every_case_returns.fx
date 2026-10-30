cbuffer c : register(b0)
{
	uint mode;
	float4 tint;
};

SamplerState s;
Texture2D t;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = t.Sample(s, texcoord).xwyz;
	switch (mode) {
		case 2:
			return float4(dot(t0.xzw, float3(0.298999995, 0.587000012, 0.114000000)), dot(t0.xzw, float3(0.298999995, 0.587000012, 0.114000000)), dot(t0.xzw, float3(0.298999995, 0.587000012, 0.114000000)), t0.y);
		default:
			return t0.xzwy * tint;
	}
}
