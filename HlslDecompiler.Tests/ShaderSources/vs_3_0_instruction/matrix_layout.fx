float4x4 view : register(c4);
row_major float4x4 world;

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	r0 = world[1] * position.y;
	r0 = position.x * world[0] + r0;
	r0 = position.z * world[2] + r0;
	r0 = position.w * world[3] + r0;
	o.x = dot(r0, transpose(view)[0]);
	o.y = dot(r0, transpose(view)[1]);
	o.z = dot(r0, transpose(view)[2]);
	o.w = dot(r0, transpose(view)[3]);

	return o;
}
