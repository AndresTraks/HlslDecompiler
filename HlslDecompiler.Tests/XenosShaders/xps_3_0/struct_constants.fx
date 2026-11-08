struct struct1
{
	float4 dir;
	float4 col;
};

struct1 lights[3];

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	for (int i = 0; i < 3; i++) {
		t0 = t0 + saturate(dot(texcoord, lights[i].dir.xyz)) * lights[i].col;
	}
	return t0;
}
