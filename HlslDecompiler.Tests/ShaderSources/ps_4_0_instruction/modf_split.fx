float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float r0;
	r0 = trunc(sv_position.x);
	o.xzw = -(r0.xxx) + sv_position.xxx;
	o.y = r0.x;

	return o;
}
