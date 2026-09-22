Texture2D scene;
StructuredBuffer<uint4> atlas : register(t1);
RWTexture2D<float4> marked : register(u1);
RWBuffer<uint4> tiles : register(u2);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	int t1 = (int)sv_position.x;
	int t0 = (int)sv_position.y;
	float4 t2 = scene.Load(int3(t1, t0, 0)) + marked[int2(t1, t0)] + (float4)(atlas[t1].w) + (float4)(tiles[t0].x) * sv_position.w;
	marked[int2(t1, t0)] = t2;
	return t2;
}
