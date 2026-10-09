float4x4 viewProjection;

struct ParticlesElement
{
	float3 position;
	float life;
	float3 velocity;
	float size;
};

StructuredBuffer<ParticlesElement> particles : register(t0);

struct VS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

VS_OUT main(uint sv_vertexid : SV_VertexID)
{
	VS_OUT o;

	uint t0 = sv_vertexid >> 2;
	float2 t1 = sv_vertexid & int2(1, 2) ? 1.0 : -1.0;
	float t2 = particles[t0].position.z;
	float t3 = particles[t0].size;
	float2 t4 = t1 * t3 + particles[t0].position.xy;
	o.sv_position = mul(float4(t4, t2, 1), viewProjection);
	o.texcoord = t1 * t3;
	o.texcoord1 = saturate(particles[t0].life);

	return o;
}
