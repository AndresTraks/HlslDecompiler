Texture2D tex;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	int4 r0;
	r0.xy = asint(sv_position.xy + sv_position.xy);
	r0.xy = (int2)asfloat(r0.xy);
	r0.zw = int2(0, 0);
	r0.x = asint(tex.Load(r0.xyz).z);
	o.y = asfloat(r0.x);
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r0.x = asint(tex.Load(r0.xyz).y);
	o.x = asfloat(r0.x);
	o.zw = float2(0, 1);

	return o;
}
