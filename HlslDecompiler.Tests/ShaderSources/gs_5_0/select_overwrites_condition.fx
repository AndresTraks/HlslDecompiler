struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint bits : BITS;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], uint sv_primitiveid : SV_PrimitiveID, inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	bool t0 = sv_primitiveid == 3;
	uint t1 = t0 ? 1088421888 : asuint(i[0].sv_position.y);
	o.sv_position = float4(i[0].sv_position.x, asfloat(i[0].sv_position.y), i[0].sv_position.z, asfloat(t1 ^ -2147483648));
	o.bits = t1 >> 30;
	stream.Append(o);
	uint t3 = t0 ? 1088421888 : asuint(i[1].sv_position.y);
	uint t2 = t0 ? 1088421888 : asuint(i[2].sv_position.y);
	o.sv_position = float4(i[1].sv_position.x, asfloat(i[1].sv_position.y), i[1].sv_position.z, asfloat(t3 ^ -2147483648));
	o.bits = t3 >> 30;
	stream.Append(o);
	o.sv_position = float4(i[2].sv_position.x, asfloat(i[2].sv_position.y), i[2].sv_position.z, asfloat(t2 ^ -2147483648));
	o.bits = t2 >> 30;
	stream.Append(o);
}
