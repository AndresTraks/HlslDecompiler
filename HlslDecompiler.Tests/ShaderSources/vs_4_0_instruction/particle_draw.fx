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

	int4 r0;
	float4 r1;
	float4 r2;
	r0.xy = sv_vertexid & int2(1, 2);
	r0.xy = (r0.xy != 0) ? int2(1, 1) : int2(-1, -1);
	r0.xy = asint((float2)r0.xy);
	r0.z = (uint)sv_vertexid >> 2;
	r1.x = particles[r0.z].size;
	r2 = float4(particles[r0.z].position, particles[r0.z].life);
	r0.xy = asint(asfloat(r0.xy) * r1.xx);
	r0.z = 0;
	r1.xyz = asfloat(r0.xyz) + r2.xyz;
	r0.w = asint(saturate(r2.w));
	o.texcoord = asfloat(r0.xy);
	o.texcoord1 = asfloat(r0.w);
	r1.w = 1;
	o.sv_position.x = dot(r1, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r1, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r1, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r1, transpose(viewProjection)[3]);

	return o;
}
