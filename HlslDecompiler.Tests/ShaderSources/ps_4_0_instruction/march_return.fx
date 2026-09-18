float4 limits;

SamplerState samp;
Texture2D heights;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	r0.xy = texcoord.xy;
	r0.zw = int2(0, 0);
	while (true) {
		r1.x = (r0.w >= 16) ? -1 : 0;
		if (asint(r1.x) != 0) break;
		r1 = heights.SampleLevel(samp, r0.xy, 0);
		r0.z = r1.x * limits.x + r0.z;
		r1.x = (limits.y < r0.z) ? -1 : 0;
		if (asint(r1.x) != 0) {
			o.y = (float)(int)r0.w;
			o.zw = float2(1, 1);
			o.x = r0.z;
			return o;
		}
		r0.xy = r0.xy + limits.zw;
		r0.w = r0.w + 1;
	}
	o.x = r0.z;
	o.yzw = float3(16, 0, 1);

	return o;
}
