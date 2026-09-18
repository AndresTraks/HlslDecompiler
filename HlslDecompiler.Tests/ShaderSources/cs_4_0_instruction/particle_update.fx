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
	float4 r0;
	float4 r1;
	float4 r2;
	r0 = float4(particles[sv_dispatchthreadid.x].position, particles[sv_dispatchthreadid.x].life);
	r1 = float4(particles[sv_dispatchthreadid.x].velocity, particles[sv_dispatchthreadid.x].size);
	r2.xz = float2(0, 0);
	r2.y = simulation.x * -(simulation.y);
	r2.xyz = r1.xyz + r2.xyz;
	r2.w = r1.w * simulation.z;
	r1.xyz = r2.xyz * simulation.xxx + r0.xyz;
	r1.w = r0.w + -(simulation.x);
	particles[sv_dispatchthreadid.x].position = r1.xyz;
	particles[sv_dispatchthreadid.x].life = r1.w;
	particles[sv_dispatchthreadid.x].velocity = r2.xyz;
	particles[sv_dispatchthreadid.x].size = r2.w;
}
