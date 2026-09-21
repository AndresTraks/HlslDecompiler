cbuffer Camera : register(b0)
{
	float4x4 viewProjection;
	float3 cameraRight;
	float spriteSize;
	float3 cameraUp;
	float fadeDistance;
};

struct GS_IN
{
	float3 position : POSITION;
	float texcoord : TEXCOORD;
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

	if (i[0].texcoord <= 0) {
		return;
	}
	float2 t0 = float2(saturate(i[0].texcoord) * spriteSize, saturate(i[0].texcoord / fadeDistance) * i[0].color.w);
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		o.sv_position = mul(float4((cameraRight * (2 * (float3)(t1 & 1) - 1) + (2 * (float3)(t1 >> 1) - 1) * cameraUp) * t0.x + i[0].position, 1), viewProjection);
		o.texcoord = 0.5 * (2 * float2((float)(t1 & 1), (float)(t1 >> 1)) - 1) + 0.5;
		o.color = float4(i[0].color.xyz, t0.y);
		stream.Append(o);
	}
	stream.RestartStrip();
}
