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

	int4 r0;
	float4 r1;
	float4 r2;
	float3 r3;
	r0.x = (0 >= i[0].texcoord.x) ? -1 : 0;
	if (r0.x != 0) {
		return;
	}
	r0.x = asint(saturate(i[0].texcoord.x));
	r0.x = asint(asfloat(r0.x) * spriteSize);
	r0.y = asint(saturate(i[0].texcoord.x / fadeDistance));
	r0.y = asint(asfloat(r0.y) * i[0].color.w);
	r1.w = 1;
	r0.z = 0;
	while (true) {
		r0.w = (r0.z >= 4) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = r0.z & 1;
		r2.x = r0.z >> 1;
		r3.x = (float)r0.w;
		r3.y = (float)(int)r2.x;
		r2.xy = r3.xy * float2(2, 2) + float2(-1, -1);
		r3 = r2.yyy * cameraUp.xyz;
		r3 = cameraRight.xyz * r2.xxx + r3.xyz;
		r1.xyz = r3.xyz * asfloat(r0.xxx) + i[0].position.xyz;
		r0.w = asint(dot(r1, transpose(viewProjection)[0]));
		r2.z = dot(r1, transpose(viewProjection)[1]);
		r2.w = dot(r1, transpose(viewProjection)[2]);
		r1.x = dot(r1, transpose(viewProjection)[3]);
		r1.yz = r2.xy * float2(0.5, 0.5) + float2(0.5, 0.5);
		o.sv_position.x = asfloat(r0.w);
		o.sv_position.y = r2.z;
		o.sv_position.z = r2.w;
		o.sv_position.w = r1.x;
		o.texcoord = r1.yz;
		o.color.xyz = i[0].color.xyz;
		o.color.w = asfloat(r0.y);
		stream.Append(o);
		r0.z = r0.z + 1;
	}
	stream.RestartStrip();
}
