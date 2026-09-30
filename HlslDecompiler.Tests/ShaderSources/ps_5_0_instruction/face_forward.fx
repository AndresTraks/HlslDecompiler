float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float2 r0;
	r0.x = dot(sv_position.xy, sv_position.xy);
	r0.x = r0.x + 1;
	r0.y = sqrt(r0.x);
	r0.x = rsqrt(r0.x);
	o.z = -(r0.x);
	o.xy = -(sv_position.xy) / r0.yy;
	o.w = 1;

	return o;
}
