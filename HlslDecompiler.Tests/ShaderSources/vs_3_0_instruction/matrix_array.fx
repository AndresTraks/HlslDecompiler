float4x4 bones[4];
int idx;

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float r0;
	int a0;
	r0 = idx.x;
	r0 = r0.x * 4;
	a0 = r0.x;
	o.x = dot(position, transpose(bones)[0][a0]);
	o.y = dot(position, transpose(bones)[1][a0]);
	o.z = dot(position, transpose(bones)[2][a0]);
	o.w = dot(position, transpose(bones)[3][a0]);

	return o;
}
