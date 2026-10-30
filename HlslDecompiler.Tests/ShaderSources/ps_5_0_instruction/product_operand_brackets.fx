float4 main(float2 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float3 r0;
	r0.x = texcoord.x * texcoord.x;
	r0.yz = -(texcoord.xy) + float2(1, 1);
	r0.yz = r0.yz * r0.yz;
	r0 = r0.xyz * texcoord.xxy;
	r0.yz = r0.yz * float2(3, 3);
	o.y = r0.z * r0.x;
	o.x = r0.z * r0.y;
	o.z = r0.y;
	o.w = texcoord.y;

	return o;
}
