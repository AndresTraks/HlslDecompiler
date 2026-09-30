float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int3 r0;
	float2 r1;
	r0.x = asint(sv_position.x) & 2139095040;
	r0.x = r0.x + -1056964608;
	r0.y = (sv_position.x != 0) ? -1 : 0;
	r0.x = r0.x & r0.y;
	r0.x = r0.x >> 23;
	r1.y = (float)r0.x;
	r0.x = asint(exp2(r1.y));
	r0.z = (1056964608 & ~(((1 << 23) - 1) << 0)) | ((asint(sv_position.x) << 0) & (((1 << 23) - 1) << 0));
	r0.y = r0.z & r0.y;
	r1.x = (float)r0.y;
	r0.y = r0.y << 3;
	o.z = (float)r0.y;
	o.w = asfloat(r0.x) * r1.x;
	o.xy = r1.xy;

	return o;
}
