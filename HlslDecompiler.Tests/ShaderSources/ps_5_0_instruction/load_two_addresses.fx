Texture2D tex;

float4 main(noperspective float4 sv_position : SV_Position) : SV_Target
{
	float4 o;

	float4 r0;
	r0.xy = sv_position.xy + sv_position.xy;
	r0.xy = (int2)r0.xy;
	r0.zw = int2(0, 0);
	r0.x = tex.Load(r0.xyz).z;
	o.y = r0.x;
	r0.xy = (int2)sv_position.xy;
	r0.zw = int2(0, 0);
	r0.x = tex.Load(r0.xyz).y;
	o.x = r0.x;
	o.zw = float2(0, 1);

	return o;
}
