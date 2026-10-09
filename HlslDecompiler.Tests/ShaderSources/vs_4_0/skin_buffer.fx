float4x4 viewProjection;

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
		int t2 = i.blendindices[t1];
		t0 = t0 + float3(dot(bones.Load(3 * t2), i.position), dot(bones.Load(3 * t2 + 1), i.position), dot(bones.Load(3 * t2 + 2), i.position)) * i.blendweight[t1];
		t1 = t1 + 1;
	}
	return mul(float4(t0, 1), viewProjection);
}
