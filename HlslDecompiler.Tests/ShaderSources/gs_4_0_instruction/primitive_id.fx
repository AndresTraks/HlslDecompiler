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

	int3 r0;
	float4 r1;
	r0 = sv_primitiveid & int3(3, 1, 2);
	r0.yz = (r0.yz != 0) ? int2(1, 1) : int2(0, 0);
	r1.xy = (float2)(int2)r0.yz;
	r1.zw = float2(0, 1);
	r0.y = 0;
	[loop]
	while (true) {
		r0.z = (r0.y >= 3) ? -1 : 0;
		if (r0.z != 0) break;
		o.sv_position = i[r0.y].sv_position;
		o.color = r1;
		o.sv_rendertargetarrayindex = r0.x;
		stream.Append(o);
		r0.y = r0.y + 1;
	}
}
