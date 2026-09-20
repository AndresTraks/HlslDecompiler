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
	float2 t1 = (float2)(sv_vertexid & int2(1, 2) ? 1 : -1);
	float4 t2 = float4(particles[t0].position, particles[t0].life);
	float2 t3 = t1 * particles[t0].size + t2.xy;
	o.sv_position = mul(float4(t3, t2.z, 1), viewProjection);
	o.texcoord = t1 * particles[t0].size;
	o.texcoord1 = saturate(t2.w);

	return o;
}
