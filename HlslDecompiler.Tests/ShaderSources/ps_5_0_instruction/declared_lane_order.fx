RWTexture2D<uint> rw : register(u1);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int3 r0;
	r0.xy = (uint2)sv_position.yx;
	r0.z = rw[r0.yx].x;
	InterlockedMax(rw[r0.xy], (uint)5);
	o.y = (float)(uint)r0.z;
	o.x = asfloat(r0.z);
	o.zw = float2(0, 1);

	return o;
}
