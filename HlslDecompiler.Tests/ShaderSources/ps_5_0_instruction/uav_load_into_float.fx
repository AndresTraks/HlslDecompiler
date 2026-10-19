Texture2D tx;
RWTexture2D<uint> rw : register(u1);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	int r1;
	r0.xy = (uint2)sv_position.yx;
	r1 = rw[r0.yx].x;
	o.x = asfloat(r1.x);
	InterlockedMax(rw[r0.xy], (uint)5);
	r0.zw = int2(0, 0);
	r0.xyz = asint(tx.Load(r0.xyz).xyz);
	o.yzw = asfloat(r0.xyz);

	return o;
}
