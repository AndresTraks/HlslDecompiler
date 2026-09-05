float4x4 world;
float4x4 viewProj;

float4 main(float4 position : POSITION) : SV_Position
{
	float4 o;

	float4 r0;
	r0.x = dot(position, transpose(world)[0]);
	r0.y = dot(position, transpose(world)[1]);
	r0.z = dot(position, transpose(world)[2]);
	r0.w = dot(position, transpose(world)[3]);
	o.x = dot(r0, transpose(viewProj)[0]);
	o.y = dot(r0, transpose(viewProj)[1]);
	o.z = dot(r0, transpose(viewProj)[2]);
	o.w = dot(r0, transpose(viewProj)[3]);

	return o;
}
