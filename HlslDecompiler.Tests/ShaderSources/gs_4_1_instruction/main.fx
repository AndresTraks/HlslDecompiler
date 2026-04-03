struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	float4 color2 : COLOR2;
	float sv_clipdistance : SV_ClipDistance;
};

struct GS_OUT
{
	float4 texcoord : TEXCOORD;
	float4 color : COLOR;
	float4 color3 : COLOR3;
	float texcoord2 : TEXCOORD2;
};

[maxvertexcount(6)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	stream.RestartStrip();
	o.texcoord = i[0].sv_position;
	o.color = i[0].color;
	o.color3 = i[0].color2;
	o.texcoord2 = i[0].sv_clipdistance.x;
	stream.Append(o);
	o.texcoord = i[1].sv_position;
	o.color = i[1].color;
	o.color3 = i[1].color2;
	o.texcoord2 = i[1].sv_clipdistance.x;
	stream.Append(o);
	o.texcoord = float4(0, 0, 0, 0);
	o.color = i[2].sv_position;
	o.color3 = float4(0, 0, 0, 0);
	o.texcoord2 = 0;
	stream.Append(o);
}
