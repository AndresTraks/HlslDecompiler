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
	float t0 = particles[sv_dispatchthreadid.x].velocity.y;
	float3 t1 = float3(particles[sv_dispatchthreadid.x].velocity.x, t0 + simulation.x * -simulation.y, particles[sv_dispatchthreadid.x].velocity.z);
	float t2 = particles[sv_dispatchthreadid.x].life;
	float t3 = particles[sv_dispatchthreadid.x].position.z;
	float t4 = particles[sv_dispatchthreadid.x].position.y;
	float t5 = particles[sv_dispatchthreadid.x].position.x;
	particles[sv_dispatchthreadid.x].position = t1 * simulation.x + float3(t5, t4, t3);
	particles[sv_dispatchthreadid.x].life = t2 - simulation.x;
	float t6 = particles[sv_dispatchthreadid.x].size;
	particles[sv_dispatchthreadid.x].velocity = t1;
	particles[sv_dispatchthreadid.x].size = t6 * simulation.z;
}
