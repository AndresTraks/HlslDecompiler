cbuffer C : register(b0)
{
	float2 texel;
	float blend;
};

SamplerState lin;
Texture2D cur;
Texture2D hist;
Texture2D<float2> vel;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int3 t0 = int3((int2)i.sv_position.xy, 0);
	float3 t1 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(1, 1, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, 1, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, 1, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t2 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(0, 1, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(0, 1, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(0, 1, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t3 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(-1, 1, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(-1, 1, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(-1, 1, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t4 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(1, 0, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, 0, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, 0, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t5 = float3(dot(cur.Load(int3(t0.x, t0.y - 1, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.x, t0.y - 1, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.x, t0.y - 1, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t6 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(1, -1, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, -1, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(1, -1, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t7 = float3(dot(cur.Load(int3(t0.xy, 0) + int3(-1, 0, 0)).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(-1, 0, 0)).xz, float2(0.5, -0.5)), dot(cur.Load(int3(t0.xy, 0) + int3(-1, 0, 0)).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t8 = float3(dot(cur.Load(t0).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(t0).xz, float2(0.5, -0.5)), dot(cur.Load(t0).xzy, float3(-0.25, -0.25, 0.5)));
	float2 t9 = i.texcoord - vel.Load(t0).xy;
	float t10 = any(abs(t9 - 0.5) > 0.5) ? 1 : blend;
	int3 t11 = int3(t0.xy - 1, 0);
	float3 t12 = float3(dot(cur.Load(t11).xzy, float3(0.25, 0.25, 0.5)), dot(cur.Load(t11).xz, float2(0.5, -0.5)), dot(cur.Load(t11).xzy, float3(-0.25, -0.25, 0.5)));
	float3 t13 = hist.SampleLevel(lin, t9, 0).xyz;
	float3 t14 = lerp(clamp(min(min(min(min(min(min(min(min(min(t12, 1000000000), t5), t6), t7), t8), t4), t3), t2), t1), float3(dot(t13.xzy, float3(0.25, 0.25, 0.5)), dot(t13.xz, float2(0.5, -0.5)), dot(t13.xzy, float3(-0.25, -0.25, 0.5))), max(t1, max(t2, max(t3, max(t4, max(max(max(max(max(t12, -1000000000), t5), t6), t7), t8)))))), t8, t10);
	return float4(t14.y + t14.x - t14.z, t14.z + t14.x, t14.x - t14.y - t14.z, 1);
}
