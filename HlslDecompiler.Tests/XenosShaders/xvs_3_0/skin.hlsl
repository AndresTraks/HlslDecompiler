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
	float4 t0 = abs(i.blendindices) - frac(abs(i.blendindices));
	float4 t1 = i.blendindices.wxyz >= 0 ? t0.wxyz : -t0.wxyz;
	float3 t2 = mul(i.position, bones[t1.x]).yzx * i.blendweight.w + mul(i.position, bones[t1.w]).yzx * i.blendweight.z + mul(i.position, bones[t1.z]).yzx * i.blendweight.y + mul(i.position, bones[t1.y]).yzx * i.blendweight.x;
	return mul(float4(t2, abs(t1.x) >= 0), vp);
}
