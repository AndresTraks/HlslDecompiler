float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float r0;
	r0 = texcoord.x * texcoord.y;
	r0 = r0.x * texcoord.z;
	o.x = -(abs(r0.x));
	o.y = r0.x;
	o.zw = float2(0, 0);

	return o;
}
