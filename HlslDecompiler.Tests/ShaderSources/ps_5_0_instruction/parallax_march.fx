float scale;
int steps;

SamplerState lin;
Texture2D hmap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	int r2;
	r0.xy = i.texcoord1.xy / i.texcoord1.zz;
	r0.xy = r0.xy * scale;
	r0.z = (float)steps;
	r0.xy = r0.xy / r0.zz;
	r0.z = 1 / r0.z;
	r0.w = hmap.SampleLevel(lin, i.texcoord.xy, 0).x;
	r1.x = 0;
	r1.y = r0.w;
	r1.zw = i.texcoord.xy;
	while (true) {
		r2 = (r1.x >= r1.y) ? -1 : 0;
		if (r2.x != 0) break;
		r1.zw = -(r0.xy) + r1.zw;
		r1.y = hmap.SampleLevel(lin, r1.zw, 0).x;
		r1.x = r0.z + r1.x;
	}
	o.xyz = r1.zwx;
	o.w = 1;

	return o;
}
