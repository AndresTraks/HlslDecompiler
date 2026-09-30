float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float r0;
	o.w = 1;
	r0 = trunc(sv_position.x);
	o.xy = -(r0.xx) + sv_position.xx;
	o.z = r0.x;

	return o;
}
