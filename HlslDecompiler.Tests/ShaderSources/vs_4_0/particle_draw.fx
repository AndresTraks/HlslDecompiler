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
	float3 texcoord : TEXCOORD;
};

VS_OUT main(uint sv_vertexid : SV_VertexID)
{
	VS_OUT o;

	int t0 = (uint)sv_vertexid >> 2;
	float2 t1 = (float2)(sv_vertexid & int2(1, 2) ? 1 : -1);
	o.sv_position = mul(float4(t1 * particles[t0].size + particles[t0].position.xy, particles[t0].position.z, 1), viewProjection);
	o.texcoord = float3(t1 * particles[t0].size, saturate(particles[t0].life));

	return o;
}
