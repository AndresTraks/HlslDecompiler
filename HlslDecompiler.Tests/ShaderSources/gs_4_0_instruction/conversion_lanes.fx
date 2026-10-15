struct GS_IN
{
	float4 sv_position : SV_Position;
	uint f : F;
	float w : W;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint f : F;
	float w : W;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	int4 r0;
	float2 r1;
	r0.x = 0;
	while (true) {
		r0.y = ((uint)r0.x >= 4) ? -1 : 0;
		if (r0.y != 0) break;
		r0.y = r0.x & 1;
		r0.z = (uint)r0.x >> 1;
		r1 = (float2)(uint2)r0.yz;
		r0.yz = asint(r1.xy * float2(0.100000001, 0.100000001) + i[0].sv_position.xy);
		r0.w = r0.x << 8;
		r0.w = r0.w | i[0].f;
		r1.x = (float)(uint)r0.x;
		r1.x = r1.x * asfloat(i[0].f);
		o.sv_position.xy = asfloat(r0.yz);
		o.sv_position.zw = i[0].sv_position.zw;
		o.f = r0.w;
		o.w = r1.x;
		stream.Append(o);
		r0.x = r0.x + 1;
	}
}
