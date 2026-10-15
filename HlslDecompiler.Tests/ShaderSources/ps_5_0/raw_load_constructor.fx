ByteAddressBuffer rb : register(t0);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	uint t0 = (uint)sv_position.x * 16;
	int2 t1 = rb.Load2(t0 + 64);
	int4 t2 = rb.Load4(t0);
	return asfloat(t1.y) * float4(asfloat(t2.xy), f16tof32(t1.x), (float)(uint)(t2.w + t2.z));
}
