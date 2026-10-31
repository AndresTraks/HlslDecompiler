bool b : register(b0);
float4x4 m;

struct VS_IN
{
	float4 position : POSITION;
	float4 normal : NORMAL;
};

float4 main(VS_IN i) : POSITION
{
	float3 t0 = mul(i.position, (float4x3)m).zyx;
	float t1 = dot(transpose(m)[3], i.position);
	if (b) {
		t0 = t0 + i.normal.zyx;
	}
	return float4(t0.zyx, t1);
}
