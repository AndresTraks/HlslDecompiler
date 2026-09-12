float4 k;

struct GS_IN
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

[maxvertexcount(6)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 3; t0 = t0 + 1) {
		o.sv_position = i[t0].sv_position * k.x + k;
		o.normal = (i[t0].normal + k.xyz) * 1 / length(i[t0].normal + k.xyz);
		o.texcoord = k.zw * i[t0].texcoord;
		stream.Append(o);
	}
	stream.RestartStrip();
	for (int t1 = 2; t1 >= 0; t1 = t1 - 1) {
		o.sv_position = i[t1].sv_position - k;
		o.normal = -i[t1].normal;
		o.texcoord = i[t1].texcoord + k.xy;
		stream.Append(o);
	}
}
