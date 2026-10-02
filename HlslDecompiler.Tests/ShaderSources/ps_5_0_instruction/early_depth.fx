[earlydepthstencil]
float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int r0;
	r0 = asint(sv_position.w + -0.5);
	r0 = (asfloat(r0.x) < 0) ? -1 : 0;
	if (r0.x != 0) discard;
	o = sv_position + sv_position;

	return o;
}
