float4x4 viewProj;
float3 right;
float3 up;
float size;

struct GS_IN
{
	float3 position : POSITION;
	float4 color : COLOR;
};

struct GS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float4 color : COLOR;
};

[maxvertexcount(4)]
void main(point GS_IN i[1], inout TriangleStream<GS_OUT> stream)
{
	GS_OUT o;

	float3 t0 = -right * size + i[0].position + up * size;
	o.sv_position = mul(float4(-right * size + i[0].position + -up * size, 1), viewProj);
	o.texcoord = float2(0, 1);
	o.color = i[0].color;
	stream.Append(o);
	o.sv_position = mul(float4(t0, 1), viewProj);
	o.texcoord = 0;
	o.color = i[0].color;
	stream.Append(o);
	float t1 = right.x * size + i[0].position.x + up.x * size;
	t0.yz = right.yz * size + i[0].position.yz + up.yz * size;
	o.sv_position = mul(float4(right * size + i[0].position + -up * size, 1), viewProj);
	o.texcoord = 1;
	o.color = i[0].color;
	stream.Append(o);
	o.sv_position = mul(float4(t1, t0.yz, 1), viewProj);
	o.texcoord = float2(1, 0);
	o.color = i[0].color;
	stream.Append(o);
}
