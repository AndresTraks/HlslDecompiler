int address;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float2 r0;
	r0 = float2(-0, -1);
	r0 = r0.xy + address.xx;
	r0 = (-abs(r0.xy) >= 0) ? 1 : 0;
	r0.x = dot(texcoord.xy, r0.xy) + 0;
	o = r0.x;

	return o;
}
