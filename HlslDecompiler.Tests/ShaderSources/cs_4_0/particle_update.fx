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
	float3 t0 = float3(particles[sv_dispatchthreadid.x].velocity.x, particles[sv_dispatchthreadid.x].velocity.y + simulation.x * -simulation.y, particles[sv_dispatchthreadid.x].velocity.z);
	float4 t1 = float4(particles[sv_dispatchthreadid.x].position, particles[sv_dispatchthreadid.x].life);
	particles[sv_dispatchthreadid.x].position = t0 * simulation.x + t1.xyz;
	particles[sv_dispatchthreadid.x].life = t1.w - simulation.x;
	particles[sv_dispatchthreadid.x].velocity = t0;
	particles[sv_dispatchthreadid.x].size = particles[sv_dispatchthreadid.x].size * simulation.z;
}
