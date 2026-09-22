Texture2D scene;
StructuredBuffer<uint4> atlas : register(t1);
RWTexture2D<float4> marked : register(u1);
RWBuffer<uint4> tiles : register(u2);

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.zw = int2(0, 0);
	r0.xy = (int2)sv_position.xy;
	r1 = scene.Load(r0.xyz);
	r2 = marked[r0.xy];
	r1 = r1 + r2;
	r0.z = atlas[r0.x].w;
	r0.z = (float)(uint)r0.z;
	r1 = r0.z + r1;
	r0.z = tiles[r0.y];
	r0.z = (float)(uint)r0.z;
	r1 = r0.z * sv_position.w + r1;
	marked[r0.xy] = r1;
	o = r1;

	return o;
}
