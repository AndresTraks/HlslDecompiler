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

	float4 r0;
	o0.sv_position = i[0].sv_position;
	o0.psize = i[0].color.x;
	stream0.Append(o0);
	r0 = offsets[0] + i[0].sv_position;
	o1.sv_position = r0;
	o1.color = i[0].color.xyz;
	stream1.Append(o1);
	r0 = offsets[1] + i[1].sv_position;
	o1.sv_position = r0;
	o1.color = i[1].color.xyz;
	stream1.Append(o1);
	r0.x = (cutoff < i[2].color.z) ? -1 : 0;
	if (asint(r0.x) != 0) {
		r0.x = tag + 1;
		o2.sv_position = i[2].sv_position;
		o2.texcoord = r0.x;
		stream2.Append(o2);
	}
}
