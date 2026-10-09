float2 resolution;

Texture2D sceneTex;
Texture2D<float> depthTex;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int3 t0 = int3((int2)i.sv_position.xy, 0);
	float2 t1 = i.sv_position.xy / resolution;
	float t2 = frac(8 * t1.x) < 0.5 ? 1.0 : 0;
	return sceneTex.Load(t0) * t2 + float4(depthTex.Load(int3(t0.xy, 0) + int3(1, 0, 0)).x, t1, i.texcoord.x);
}
