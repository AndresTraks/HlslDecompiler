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

	o.sv_position = i[0].sv_position + i[0].weight[mode % 3];
	stream.Append(o);
}
