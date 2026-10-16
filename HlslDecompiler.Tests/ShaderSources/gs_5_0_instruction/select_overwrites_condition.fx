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

	int3 r0;
	o.sv_position.xyz = i[0].sv_position.xyz;
	r0.x = (sv_primitiveid == 3) ? -1 : 0;
	r0.y = (r0.x != 0) ? 1088421888 : asint(i[0].sv_position.y);
	r0.z = r0.y ^ -2147483648;
	r0.y = (uint)r0.y >> 30;
	o.sv_position.w = asfloat(r0.z);
	o.bits = r0.y;
	stream.Append(o);
	o.sv_position.xyz = i[1].sv_position.xyz;
	r0.y = (r0.x != 0) ? 1088421888 : asint(i[1].sv_position.y);
	r0.x = (r0.x != 0) ? 1088421888 : asint(i[2].sv_position.y);
	r0.z = r0.y ^ -2147483648;
	r0.y = (uint)r0.y >> 30;
	o.sv_position.w = asfloat(r0.z);
	o.bits = r0.y;
	stream.Append(o);
	o.sv_position.xyz = i[2].sv_position.xyz;
	r0.y = r0.x ^ -2147483648;
	r0.x = (uint)r0.x >> 30;
	o.sv_position.w = asfloat(r0.y);
	o.bits = r0.x;
	stream.Append(o);
}
