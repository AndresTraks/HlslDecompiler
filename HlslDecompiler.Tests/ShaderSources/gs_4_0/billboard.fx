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

	float3 t1 = -right * size + i[0].position;
	float3 t2 = -up * size + t1;
	float3 t0 = up * size + t1;
	o.sv_position = mul(float4(t2, 1), viewProj);
	o.texcoord = float2(0, 1);
	o.color = i[0].color;
	stream.Append(o);
	o.sv_position = mul(float4(t0, 1), viewProj);
	o.texcoord = 0;
	o.color = i[0].color;
	stream.Append(o);
	float3 t4 = right * size + i[0].position;
	float3 t5 = -up * size + t4;
	float t3 = up.x * size + t4.x;
	t0.yz = up.yz * size + t4.yz;
	o.sv_position = mul(float4(t5, 1), viewProj);
	o.texcoord = 1;
	o.color = i[0].color;
	stream.Append(o);
	o.sv_position = mul(float4(t3, t0.yz, 1), viewProj);
	o.texcoord = float2(1, 0);
	o.color = i[0].color;
	stream.Append(o);
}
