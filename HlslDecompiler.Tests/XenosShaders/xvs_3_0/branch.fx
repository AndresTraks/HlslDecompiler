bool b;
float4x4 m;

struct VS_IN
{
	float4 position : POSITION;
	float3 normal : NORMAL;
};

float4 main(VS_IN i) : POSITION
{
	float4 t0 = mul(i.position, m);
	if (b) {
		t0.xyz = t0.xyz + i.normal;
	}
	return t0;
}
