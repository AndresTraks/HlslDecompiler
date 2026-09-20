float4x4 viewProjection;

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
};

Buffer<float4> bones;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	uint4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : SV_Position
{
	float3 t0 = 0;
	int t1 = 0;
	while (t1 < 3) {
		int t2 = ((((uint)t1 < 3 ? -1 : 0) == 0 ? i.blendindices.w : 0) | (((uint)t1 < 2 ? 0 : t1 - 3) & i.blendindices.z)) | ((((uint)t1 < 2 ? -t1 : 0) & i.blendindices.y) | (((uint)t1 < 1 ? -1 : 0) & i.blendindices.x));
		float t3 = dot(i.blendweight, icb[t1]);
		t0 = float3(dot(bones.Load(3 * t2), i.position), dot(bones.Load(3 * t2 + 1), i.position), dot(bones.Load(3 * t2 + 2), i.position)) * t3 + t0;
		t1 = t1 + 1;
	}
	return mul(float4(t0, 1), viewProjection);
}
