float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 o;

	float3 r0;
	float3 r1;
	r0 = texcoord.wxz * texcoord.wyz;
	r0 = texcoord.zwy * texcoord.xzw + -(r0.xyz);
	r0.x = dot(r0.xyz, texcoord.xyz);
	r1.x = dot(texcoord.xyz, texcoord.xyz);
	r1.y = dot(texcoord.yzw, texcoord.xyz);
	r1.z = dot(texcoord.zwx, texcoord.xyz);
	o.xyz = r1.xyz / r0.xxx;
	o.w = r0.x;

	return o;
}
