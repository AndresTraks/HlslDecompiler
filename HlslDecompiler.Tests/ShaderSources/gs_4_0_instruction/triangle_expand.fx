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

	float4 r0;
	float4 r1;
	float2 r2;
	r0.x = 0;
	while (true) {
		r0.y = (r0.x >= 3) ? -1 : 0;
		if (r0.y != 0) break;
		r1 = i[r0.x].sv_position * k.x + k;
		r0.yzw = k.xyz + i[r0.x].normal.xyz;
		r2.x = dot(r0.yzw, r0.yzw);
		r2.x = 1 / sqrt(r2.x);
		r0.yzw = r0.yzw * r2.xxx;
		r2 = k.zw * i[r0.x].texcoord.xy;
		o.sv_position = r1;
		o.normal = r0.yzw;
		o.texcoord = r2.xy;
		stream.Append(o);
		r0.x = r0.x + 1;
	}
	stream.RestartStrip();
	r0.x = 2;
	while (true) {
		r0.y = (r0.x < 0) ? -1 : 0;
		if (r0.y != 0) break;
		r1 = -(k) + i[r0.x].sv_position;
		r0.yz = k.xy + i[r0.x].texcoord.xy;
		o.sv_position = r1;
		o.normal = -(i[r0.x].normal.xyz);
		o.texcoord = r0.yz;
		stream.Append(o);
		r0.x = r0.x + -1;
	}
}
