cbuffer Params : register(b0)
{
	float3 gravity;
	float threshold;
};

struct GS_IN
{
	float4 sv_position : SV_Position;
	float3 texcoord : TEXCOORD;
};

struct GS_OUT0
{
	float4 sv_position : SV_Position;
	float3 texcoord : TEXCOORD;
};

struct GS_OUT1
{
	float4 sv_position : SV_Position;
	float texcoord : TEXCOORD;
};

[maxvertexcount(1)]
void main(point GS_IN i[1], inout PointStream<GS_OUT0> stream0, inout PointStream<GS_OUT1> stream1)
{
	GS_OUT0 o0;
	GS_OUT1 o1;

	int4 r0;
	r0.x = asint(dot(i[0].texcoord.xyz, i[0].texcoord.xyz));
	r0.x = asint(sqrt(asfloat(r0.x)));
	r0.y = (threshold < asfloat(r0.x)) ? -1 : 0;
	if (r0.y != 0) {
		r0.yzw = asint(gravity.xyz + i[0].texcoord.xyz);
		o0.sv_position = i[0].sv_position;
		o0.texcoord = asfloat(r0.yzw);
		stream0.Append(o0);
	} else {
		o1.sv_position = i[0].sv_position;
		o1.texcoord = asfloat(r0.x);
		stream1.Append(o1);
	}
}
