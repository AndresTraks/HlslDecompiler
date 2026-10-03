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

	float4 r0;
	float4 r1;
	float2 r2;
	r0.w = 1;
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 6) ? -1 : 0;
		if (asint(r1.y) != 0) break;
		r1.y = (int)r1.x << 2;
		r1.z = 0;
		while (true) {
			r1.w = (r1.z >= 3) ? -1 : 0;
			if (asint(r1.w) != 0) break;
			r0.xyz = i[r1.z].pos.xyz;
			r1.w = dot(r0, transpose(faces[r1.y / 4])[0]);
			r2.x = dot(r0, transpose(faces[r1.y / 4])[1]);
			r2.y = dot(r0, transpose(faces[r1.y / 4])[2]);
			r0.x = dot(r0, transpose(faces[r1.y / 4])[3]);
			o.sv_position.x = r1.w;
			o.sv_position.y = r2.x;
			o.sv_position.z = r2.y;
			o.sv_position.w = r0.x;
			o.sv_rendertargetarrayindex = r1.x;
			stream.Append(o);
			r1.z = r1.z + 1;
		}
		stream.RestartStrip();
		r1.x = r1.x + 1;
	}
}
