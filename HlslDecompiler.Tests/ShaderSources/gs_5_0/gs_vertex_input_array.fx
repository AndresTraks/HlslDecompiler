struct GS_IN
{
	float4 sv_position : SV_Position;
	float weights[2] : WEIGHTS;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float2 keys[2] : KEYS;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	o.sv_position = i[0].sv_position;
	o.keys[0] = float2(i[0].weights[0], i[0].weights[1]);
	o.keys[1] = float2(i[0].weights[1], 0);
	stream.Append(o);
	o.sv_position = i[1].sv_position;
	o.keys[0] = float2(i[1].weights[0], i[1].weights[1]);
	o.keys[1] = float2(i[1].weights[1], 1);
	stream.Append(o);
	o.sv_position = i[2].sv_position;
	o.keys[0] = float2(i[2].weights[0], i[2].weights[1]);
	o.keys[1] = float2(i[2].weights[1], 2);
	stream.Append(o);
}
