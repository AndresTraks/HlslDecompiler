float4 weights;

SamplerState samp;
Texture2D source;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = float4(0, 0, 0, 0);
	r1.x = 0;
	while (true) {
		r1.y = (r1.x >= 8) ? -1 : 0;
		if (asint(r1.y) != 0) break;
		r1.y = (float)(int)r1.x;
		r1.yz = weights.zw * r1.yy + texcoord.xy;
		r2 = source.SampleLevel(samp, r1.yz, 0);
		r1.yzw = r2.xyz * weights.xxx + r0.xyz;
		r2.x = (r2.x < weights.y) ? -1 : 0;
		if (asint(r2.x) != 0) {
			r2.x = r1.x + 1;
			r0.xyz = r1.yzw;
			r1.x = r2.x;
			continue;
		}
		r0.w = r0.w + 1;
		r1.x = r1.x + 1;
		r0.xyz = r1.yzw;
	}
	r1.x = max(r0.w, 1);
	o.xyz = r0.xyz / r1.xxx;
	o.w = r0.w;

	return o;
}
