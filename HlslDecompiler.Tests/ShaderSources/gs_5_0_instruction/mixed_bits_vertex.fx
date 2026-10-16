struct GS_IN
{
	float4 position : POSITION;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint id : ID;
	float f : F;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], uint sv_primitiveid : SV_PrimitiveID, inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	int4 r0;
	int r1;
	r0.x = asint((float)(uint)sv_primitiveid);
	r0.y = 0;
	while (true) {
		r0.z = (r0.y >= 3) ? -1 : 0;
		if (r0.z != 0) break;
		r0.zw = (float2(0, 0) < i[r0.y].position.xz) ? -1 : 0;
		r1 = r0.y + sv_primitiveid;
		r0.z = (r0.z != 0) ? asint(i[r0.y].position.y) : r1.x;
		r0.w = (r0.w != 0) ? sv_primitiveid : r0.x;
		o.sv_position = i[r0.y].position;
		o.id = r0.z;
		o.f = asfloat(r0.w);
		stream.Append(o);
		r0.y = r0.y + 1;
	}
}
