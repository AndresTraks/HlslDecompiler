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

	float t0 = (float)sv_primitiveid;
	for (int t1 = 0; t1 < 3; t1 = t1 + 1) {
		o.sv_position = i[t1].position;
		o.id = i[t1].position.x > 0 ? asint(i[t1].position.y) : t1 + sv_primitiveid;
		o.f = i[t1].position.z > 0 ? asfloat(sv_primitiveid) : t0;
		stream.Append(o);
	}
}
