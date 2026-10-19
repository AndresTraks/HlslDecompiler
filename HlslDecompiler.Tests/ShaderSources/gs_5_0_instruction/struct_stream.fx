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

	int4 r0;
	int3 r1;
	o.sv_position = i[0].sv_position;
	o.texcoord = float2(0, 0);
	r0.x = i[0].id << 2;
	o.id = r0.x;
	stream.Append(o);
	r0 = asint(float4(0.100000001, 0, 0, 0.100000001) + i[0].sv_position.xyxy);
	o.sv_position.xy = asfloat(r0.xy);
	o.sv_position.zw = i[0].sv_position.zw;
	o.texcoord = float2(1, 0);
	r1 = (int3(1, 2, 3) & ~(((1 << int3(30, 30, 30)) - 1) << int3(2, 2, 2))) | ((i[0].id << int3(2, 2, 2)) & (((1 << int3(30, 30, 30)) - 1) << int3(2, 2, 2)));
	o.id = r1.x;
	stream.Append(o);
	o.sv_position.xy = asfloat(r0.zw);
	o.sv_position.zw = i[0].sv_position.zw;
	o.texcoord = float2(0, 1);
	o.id = r1.y;
	stream.Append(o);
	r0.xy = asint(float2(0.100000001, 0.100000001) + i[0].sv_position.xy);
	o.sv_position.xy = asfloat(r0.xy);
	o.sv_position.zw = i[0].sv_position.zw;
	o.texcoord = float2(1, 1);
	o.id = r1.z;
	stream.Append(o);
}
