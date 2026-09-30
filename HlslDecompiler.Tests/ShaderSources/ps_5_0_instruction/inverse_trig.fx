float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	int4 r1;
	r0.xy = asint(min(abs(sv_position.xx), float2(1, 2)));
	r0.zw = asint(max(abs(sv_position.xx), float2(1, 2)));
	r0.xy = asint(asfloat(r0.xy) / asfloat(r0.zw));
	r0.zw = asint(asfloat(r0.xy) * asfloat(r0.xy));
	r1.xy = asint(asfloat(r0.zw) * float2(0.0208350997, 0.0208350997) + float2(-0.0851330012, -0.0851330012));
	r1.xy = asint(asfloat(r0.zw) * asfloat(r1.xy) + float2(0.180141002, 0.180141002));
	r1.xy = asint(asfloat(r0.zw) * asfloat(r1.xy) + float2(-0.330299497, -0.330299497));
	r0.zw = asint(asfloat(r0.zw) * asfloat(r1.xy) + float2(0.999866009, 0.999866009));
	r1.xy = asint(asfloat(r0.zw) * asfloat(r0.xy));
	r1.xy = asint(asfloat(r1.xy) * float2(-2, -2) + float2(1.57079637, 1.57079637));
	r1.zw = (float2(1, 2) < abs(sv_position.xx)) ? -1 : 0;
	r1.xy = r1.xy & r1.zw;
	r0.xy = asint(asfloat(r0.xy) * asfloat(r0.zw) + asfloat(r1.xy));
	r0.zw = asint(min(sv_position.xx, float2(1, 2)));
	r0.zw = (asfloat(r0.zw) < float2(0, 0)) ? -1 : 0;
	o.xw = (r0.zw != 0) ? asfloat(asint(-(asfloat(r0.xy)))) : asfloat(r0.xy);
	r0.x = asint(max(sv_position.x, -1));
	r0.x = asint(min(asfloat(r0.x), 1));
	r0.y = asint(abs(asfloat(r0.x)) * -0.0187292993 + 0.0742610022);
	r0.y = asint(asfloat(r0.y) * abs(asfloat(r0.x)) + -0.212114394);
	r0.y = asint(asfloat(r0.y) * abs(asfloat(r0.x)) + 1.57072878);
	r0.z = asint(-(abs(asfloat(r0.x))) + 1);
	r0.x = (asfloat(r0.x) < 0) ? -1 : 0;
	r0.z = asint(sqrt(asfloat(r0.z)));
	r0.w = asint(asfloat(r0.z) * asfloat(r0.y));
	r0.w = asint(asfloat(r0.w) * -2 + 3.14159274);
	r0.x = r0.w & r0.x;
	r0.x = asint(asfloat(r0.y) * asfloat(r0.z) + asfloat(r0.x));
	o.yz = asfloat(r0.xx) * float2(-1, 1) + float2(1.57079637, 0);

	return o;
}
