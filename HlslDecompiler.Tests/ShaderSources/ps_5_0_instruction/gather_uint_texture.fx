SamplerState s0;
Texture2D<uint> ids;
Texture2D<int2> offs;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r0.z = offs.Load(r0.xyz).x;
	r0.x = ids.Load(r0.xyw).x;
	r0.y = asint((float)r0.z);
	o.y = asfloat(r0.y) * 0.5;
	r0.yzw = asint(ids.Gather(s0, sv_position.xy).xyw);
	r0.y = r0.w + r0.y;
	o.w = asfloat(r0.z ^ r0.x);
	o.x = asfloat(r0.x);
	o.z = (float)(uint)r0.y;

	return o;
}
