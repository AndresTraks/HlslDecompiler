struct GS_IN
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	uint id : ID;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	uint id : ID;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	o.sv_position = i[0].sv_position;
	o.texcoord = 0;
	o.id = i[0].id * 4;
	stream.Append(o);
	int2 t0 = (int2(2, 3) & 3) | ((i[0].id << 2) & -4);
	float2 t1 = i[0].sv_position.xy + float2(0, 0.100000001);
	o.sv_position = i[0].sv_position + float4(0.100000001, 0, 0, 0);
	o.texcoord = float2(1, 0);
	o.id = (1 & 3) | ((i[0].id << 2) & -4);
	stream.Append(o);
	o.sv_position = float4(t1, i[0].sv_position.zw);
	o.texcoord = float2(0, 1);
	o.id = t0.x;
	stream.Append(o);
	o.sv_position = i[0].sv_position + float4(0.100000001, 0.100000001, 0, 0);
	o.texcoord = 1;
	o.id = t0.y;
	stream.Append(o);
}
