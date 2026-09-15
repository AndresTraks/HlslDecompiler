struct GS_IN
{
	float4 sv_position : SV_Position;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	uint sv_rendertargetarrayindex : SV_RenderTargetArrayIndex;
};

[maxvertexcount(3)]
void main(triangle GS_IN i[3], uint sv_primitiveid : SV_PrimitiveID, inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	int t0 = sv_primitiveid & 3;
	float2 t1 = (float2)(sv_primitiveid & int2(1, 2) ? 1 : 0);
	[loop]
	for (int t2 = 0; t2 < 3; t2 = t2 + 1) {
		o.sv_position = i[t2].sv_position;
		o.color = float4(t1, 0, 1);
		o.sv_rendertargetarrayindex = t0;
		stream.Append(o);
	}
}
