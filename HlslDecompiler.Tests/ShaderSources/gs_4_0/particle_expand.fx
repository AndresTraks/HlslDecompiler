float4x4 viewProj;
float3 right;
float3 up;

struct GS_IN
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	float psize : PSIZE;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float4 color : COLOR;
	float2 texcoord : TEXCOORD;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	float3 t0 = up * i[0].psize;
	o.sv_position = mul(float4(-right * i[0].psize - t0 + i[0].sv_position.xyz, 1), viewProj);
	o.color = i[0].color;
	o.texcoord = 0;
	stream.Append(o);
	t0.x = right.x * i[0].psize + t0.x + i[0].sv_position.x;
	o.sv_position = mul(float4(right * i[0].psize - t0 + i[0].sv_position.xyz, 1), viewProj);
	o.color = i[0].color;
	o.texcoord = float2(1, 0);
	stream.Append(o);
	o.sv_position = mul(float4(up * i[0].psize - right * i[0].psize + i[0].sv_position.xyz, 1), viewProj);
	o.color = i[0].color;
	o.texcoord = float2(0, 1);
	stream.Append(o);
	o.sv_position = mul(float4(t0.x, right.yz * i[0].psize + t0.yz + i[0].sv_position.yz, 1), viewProj);
	o.color = i[0].color;
	o.texcoord = 1;
	stream.Append(o);
}
