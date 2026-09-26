cbuffer Params : register(b0)
{
	float4 offsets[3];
	float cutoff;
	uint tag;
};

struct GS_IN
{
	float4 sv_position : SV_Position;
	float3 color : COLOR;
};

struct GS_OUT0
{
	float4 sv_position : SV_Position;
	float psize : PSIZE;
};

struct GS_OUT1
{
	float4 sv_position : SV_Position;
	float3 color : COLOR;
};

struct GS_OUT2
{
	float4 sv_position : SV_Position;
	uint texcoord : TEXCOORD;
};

[maxvertexcount(6)]
void main(triangle GS_IN i[3], inout PointStream<GS_OUT0> stream0, inout PointStream<GS_OUT1> stream1, inout PointStream<GS_OUT2> stream2)
{
	GS_OUT0 o0;
	GS_OUT1 o1;
	GS_OUT2 o2;

	o0.sv_position = i[0].sv_position;
	o0.psize = i[0].color.x;
	stream0.Append(o0);
	o1.sv_position = i[0].sv_position + offsets[0];
	o1.color = i[0].color;
	stream1.Append(o1);
	o1.sv_position = i[1].sv_position + offsets[1];
	o1.color = i[1].color;
	stream1.Append(o1);
	if (cutoff < i[2].color.z) {
		o2.sv_position = i[2].sv_position;
		o2.texcoord = 1 + tag;
		stream2.Append(o2);
	}
}
