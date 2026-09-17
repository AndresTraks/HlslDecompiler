float4x4 faceViewProjection[6];

struct GS_IN
{
	float4 texcoord : TEXCOORD;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float3 texcoord : TEXCOORD;
	uint sv_rendertargetarrayindex : SV_RenderTargetArrayIndex;
};

[maxvertexcount(18)]
void main(triangle GS_IN i[3], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	for (int t0 = 0; t0 < 6; t0 = t0 + 1) {
		int t1 = t0 * 4;
		for (int t2 = 0; t2 < 3; t2 = t2 + 1) {
			o.sv_position = mul(i[t2].texcoord, faceViewProjection[t1 / 4]);
			o.texcoord = i[t2].texcoord.xyz;
			o.sv_rendertargetarrayindex = t0;
			stream.Append(o);
		}
		stream.RestartStrip();
	}
}
