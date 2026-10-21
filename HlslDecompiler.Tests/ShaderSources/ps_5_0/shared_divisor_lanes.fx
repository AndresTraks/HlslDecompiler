float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float3 t0 = cross(texcoord.yzw, texcoord.zwx);
	return float4(float3(dot(texcoord.xyz, texcoord.xyz), dot(texcoord.yzw, texcoord.xyz), dot(texcoord.zwx, texcoord.xyz)) / (dot(t0, texcoord.xyz)), dot(t0, texcoord.xyz));
}
