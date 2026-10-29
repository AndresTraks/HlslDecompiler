cbuffer C : register(b0)
{
	int mode;
};

struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 weight[3] : WEIGHT;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
};

[maxvertexcount(1)]
void main(point GS_IN i[1], inout PointStream<GS_OUT> stream)
{
	GS_OUT o;

	int4 r0;
	r0.x = mode & -2147483648;
	r0.y = max(mode, -(mode));
	r0.y = (uint)r0.y % 3;
	r0.z = -r0.y;
	r0.x = (r0.x != 0) ? r0.z : r0.y;
	r0 = asint(i[0].sv_position + i[0].weight[r0.x]);
	o.sv_position = asfloat(r0);
	stream.Append(o);
}
