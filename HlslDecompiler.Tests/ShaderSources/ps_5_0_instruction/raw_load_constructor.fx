ByteAddressBuffer rb : register(t0);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int2 r0;
	int4 r1;
	float4 r2;
	r0.x = (uint)sv_position.x;
	r0.x = r0.x << 4;
	r0.y = r0.x + 64;
	r1 = rb.Load4(r0.x);
	r0 = rb.Load2(r0.y);
	r2.z = f16tof32(r0.x);
	r0.x = r1.w + r1.z;
	r2.xy = (float2)r1.xy;
	r2.w = (float)(uint)r0.x;
	o = asfloat(r0.y) * r2;

	return o;
}
