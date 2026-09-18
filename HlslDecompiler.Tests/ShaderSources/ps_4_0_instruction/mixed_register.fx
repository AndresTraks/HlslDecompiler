float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xyz = int3(0, 0, 0);
	while (true) {
		r0.w = (r0.z >= steps) ? -1 : 0;
		if (asint(r0.w) != 0) break;
		r0.w = (float)(int)r0.z;
		r1.xy = k.xy * r0.ww + texcoord.xy;
		r1 = tex.SampleLevel(samp, r1.xy, 0);
		r0.w = (k.z < r1.x) ? -1 : 0;
		r1.x = r0.x + r1.x;
		r1.y = r0.y + 1;
		r0.xy = (asint(r0.ww) != 0) ? r1.xy : r0.xy;
		r0.z = r0.z + 1;
	}
	r1.y = (float)(int)r0.y;
	r0.y = max(r1.y, 1);
	r1.x = r0.x / r0.y;
	o.z = saturate(r1.x);
	o.w = k.w;
	o.xy = r1.xy;

	return o;
}
