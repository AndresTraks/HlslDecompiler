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

	float4 r0;
	float3 r1;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 6) ? -1 : 0;
		if (asint(r0.y) != 0) break;
		r0.y = (int)r0.x << 2;
		r0.z = 0;
		while (true) {
			r0.w = (r0.z >= 3) ? -1 : 0;
			if (asint(r0.w) != 0) break;
			r0.w = dot(i[r0.z].texcoord, transpose(faceViewProjection[r0.y / 4])[0]);
			r1.x = dot(i[r0.z].texcoord, transpose(faceViewProjection[r0.y / 4])[1]);
			r1.y = dot(i[r0.z].texcoord, transpose(faceViewProjection[r0.y / 4])[2]);
			r1.z = dot(i[r0.z].texcoord, transpose(faceViewProjection[r0.y / 4])[3]);
			o.sv_position.x = r0.w;
			o.sv_position.y = r1.x;
			o.sv_position.z = r1.y;
			o.sv_position.w = r1.z;
			o.texcoord = i[r0.z].texcoord.xyz;
			o.sv_rendertargetarrayindex = r0.x;
			stream.Append(o);
			r0.z = r0.z + 1;
		}
		stream.RestartStrip();
		r0.x = r0.x + 1;
	}
}
