float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float r0;
	r0.x = dot(position.xyz, position.xyz);
	r0.x = 1 / sqrt(r0.x);
	o.xyz = r0.xxx * position.yxz;
	o.w = 1;

	return o;
}
