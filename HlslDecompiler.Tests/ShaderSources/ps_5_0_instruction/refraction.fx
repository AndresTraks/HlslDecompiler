float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float3 r0;
	r0.x = dot(sv_position.xy, sv_position.xy);
	r0.x = r0.x + 1;
	r0.x = sqrt(r0.x);
	r0.yz = sv_position.xy / r0.xx;
	o.z = 0.899999976 / r0.x;
	r0.x = -(r0.z) * r0.z + 1;
	r0.x = r0.x * -0.809999943 + 1;
	r0.x = sqrt(r0.x);
	r0.x = r0.z * 0.899999976 + r0.x;
	o.y = r0.z * 0.899999976 + -(r0.x);
	r0.x = r0.y * 0.899999976;
	o.x = r0.x;
	o.w = 1;

	return o;
}
