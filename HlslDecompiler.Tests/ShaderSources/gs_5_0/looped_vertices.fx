cbuffer Faces : register(b0)
{
	float4x4 faces[6];
};

struct GS_IN
{
	float3 pos : POS;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	uint sv_rendertargetarrayindex : SV_RenderTargetArrayIndex;
};

[maxvertexcount(18)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 6; t0 = t0 + 1) {
		for (int t1 = 0; t1 < 3; t1 = t1 + 1) {
			o.sv_position = mul(float4(i[t1].pos, 1), faces[t0]);
			o.sv_rendertargetarrayindex = t0;
			stream.Append(o);
		}
		stream.RestartStrip();
	}
}
