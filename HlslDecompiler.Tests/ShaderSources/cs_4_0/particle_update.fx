float4 simulation;

struct ParticlesElement
{
	float3 position;
	float life;
	float3 velocity;
	float size;
};

RWStructuredBuffer<ParticlesElement> particles : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float3 t0 = particles[sv_dispatchthreadid.x].velocity;
	float3 t1 = float3(t0.x, t0.y + simulation.x * -simulation.y, t0.z);
	float4 t2 = float4(particles[sv_dispatchthreadid.x].position, particles[sv_dispatchthreadid.x].life);
	particles[sv_dispatchthreadid.x].position = t1 * simulation.x + t2.xyz;
	particles[sv_dispatchthreadid.x].life = t2.w - simulation.x;
	float t3 = particles[sv_dispatchthreadid.x].size;
	particles[sv_dispatchthreadid.x].velocity = t1;
	particles[sv_dispatchthreadid.x].size = t3 * simulation.z;
}
