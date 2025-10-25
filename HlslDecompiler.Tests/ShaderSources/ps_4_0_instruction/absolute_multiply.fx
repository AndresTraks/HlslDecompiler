float4 main(float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float r0;
	r0 = sv_position.x * 3;
	o.w = abs(r0.x);
	o.xy = sv_position.xw * float4(3, 3) + float4(-1, -1);
	o.z = 8;

	return o;
}
