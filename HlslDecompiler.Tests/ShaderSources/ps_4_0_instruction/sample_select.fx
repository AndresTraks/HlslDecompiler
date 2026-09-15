float4 k;
int steps;

SamplerState samp;
Texture2D tex;

float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float3 r2;
	float4 r3;
	r0.xy = texcoord.xy * k.xy;
	r0.zw = texcoord.xy + k.zw;
	r1 = float4(0, 0, 0, 0);
	r2.x = asfloat(0);
	[loop]
	while (true) {
		r2.y = asfloat((asint(r2.x) >= steps) ? -1 : 0);
		if (asint(r2.y) != 0) break;
		r2.y = asfloat(asint(r2.x) & 1);
		r2.yz = (asint(r2.yy) != 0) ? r0.xy : r0.zw;
		r3 = tex.Sample(samp, r2.yz);
		r2.y = asfloat((r3.w < 0.5) ? -1 : 0);
		if (asint(r2.y) != 0) {
			break;
		}
		r2.y = asfloat(asint(r2.x) + 1);
		r2.y = (float)asint(r2.y);
		r1 = r3 * r2.y + r1;
		r2.x = asfloat(asint(r2.x) + 1);
	}
	r0.x = steps + 1;
	r0.x = (float)(int)r0.x;
	o = r1 / r0.x;

	return o;
}
