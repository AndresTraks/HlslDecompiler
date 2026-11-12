float4 k;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float3 r1;
	float4 r2;
	r0 = 0;
	r1.x = 0;
	for (int i0 = 0; i0 < 4; i0++) {
		r1.yz = r1.xx * k.xy + texcoord.xy;
		r2 = tex2D(s0, r1.yz);
		r0 = half4(r2 * k.z + r0);
		r1.x = r1.x + 1;
	}
	o = r0;

	return o;
}
