float4 k;
float3 n;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = abs(dot(texcoord, texcoord));
	float2 t1 = texcoord.xy * rsqrt(t0);
	return float4(dot(k, texcoord) + t1.x, dot(n, texcoord.xyz) + t1.y, sqrt(abs(dot(texcoord.xy, texcoord.xy))) + texcoord.z * rsqrt(t0), sqrt(abs(dot(texcoord.zxy - n.zxy, texcoord.zxy - n.zxy))) + texcoord.w * rsqrt(t0));
}
