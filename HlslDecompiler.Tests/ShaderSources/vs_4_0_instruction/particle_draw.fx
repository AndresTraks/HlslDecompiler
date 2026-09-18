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

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = asfloat(sv_vertexid.xx & int2(1, 2));
	r0.xy = (asint(r0.xy) != 0) ? asfloat(int2(1, 1)) : asfloat(int2(-1, -1));
	r0.xy = (float2)asint(r0.xy);
	r0.z = (uint)sv_vertexid.x >> 2;
	r1.x = particles[r0.z].size;
	r2 = float4(particles[r0.z].position, particles[r0.z].life);
	r0.xy = r0.xy * r1.xx;
	r0.z = 0;
	r1.xyz = r0.xyz + r2.xyz;
	r0.w = saturate(r2.w);
	o.texcoord = r0.xy;
	o.texcoord1 = r0.w;
	r1.w = 1;
	o.sv_position.x = dot(r1, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r1, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r1, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r1, transpose(viewProjection)[3]);

	return o;
}
