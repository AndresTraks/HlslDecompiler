float4x3 bones[20];
float4x4 vp;

struct VS_IN
{
	float4 position : POSITION;
	float4 blendweight : BLENDWEIGHT;
	float4 blendindices : BLENDINDICES;
};

float4 main(VS_IN i) : POSITION
{
	float3 t0 = 0;
	for (int i_ = 0; i_ < 4; i_++) {
		t0 = t0 + mul(i.position, bones[(int)i.blendindices[i_]]) * i.blendweight[i_];
	}
	return mul(float4(t0, 1), vp);
}
