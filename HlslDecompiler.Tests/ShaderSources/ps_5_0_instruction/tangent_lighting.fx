float3 lightDirection;
float lightIntensity;
float3 cameraPosition;
uint flags;
float4 fogColor;
float2 fogRange;

SamplerState linearSampler;
Texture2D albedoMap;
Texture2D normalMap;

struct PS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int4 r0;
	float4 r1;
	float4 r2;
	float3 r3;
	r0.x = asint(dot(i.normal.xyz, i.normal.xyz));
	r0.x = asint(rsqrt(asfloat(r0.x)));
	r0.xyz = asint(asfloat(r0.xxx) * i.normal.xyz);
	r0.w = asint(dot(i.tangent.xyz, asfloat(r0.xyz)));
	r1.xyz = -(asfloat(r0.xyz)) * asfloat(r0.www) + i.tangent.xyz;
	r0.w = asint(dot(r1.xyz, r1.xyz));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r1.xyz = asfloat(r0.www) * r1.xyz;
	r2.xyz = asfloat(r0.zxy) * r1.yzx;
	r2.xyz = asfloat(r0.yzx) * r1.zxy + -(r2.xyz);
	r3 = normalMap.Sample(linearSampler, i.texcoord.xy).xyz;
	r3 = r3.xyz * float3(2, 2, 2) + float3(-1, -1, -1);
	r2.xyz = r2.xyz * r3.yyy;
	r1.xyz = r3.xxx * r1.xyz + r2.xyz;
	r0.xyz = asint(r3.zzz * asfloat(r0.xyz) + r1.xyz);
	r0.w = asint(dot(asfloat(r0.xyz), asfloat(r0.xyz)));
	r0.w = asint(rsqrt(asfloat(r0.w)));
	r0.xyz = asint(asfloat(r0.www) * asfloat(r0.xyz));
	r0.w = asint(saturate(dot(asfloat(r0.xyz), -(lightDirection.xyz))));
	r0.w = asint(asfloat(r0.w) * lightIntensity);
	r1.xyz = -(i.position.xyz) + cameraPosition.xyz;
	r1.w = dot(r1.xyz, r1.xyz);
	r2.x = rsqrt(r1.w);
	r1.w = sqrt(r1.w);
	r1.w = r1.w + -(fogRange.x);
	r1.xyz = r1.xyz * r2.xxx + -(lightDirection.xyz);
	r2.x = dot(r1.xyz, r1.xyz);
	r2.x = rsqrt(r2.x);
	r1.xyz = r1.xyz * r2.xxx;
	r0.x = asint(saturate(dot(asfloat(r0.xyz), r1.xyz)));
	r0.x = asint(log2(asfloat(r0.x)));
	r0.x = asint(asfloat(r0.x) * 32);
	r0.x = asint(exp2(asfloat(r0.x)));
	r0.yz = asint(flags) & int2(1, 2);
	r0.y = (r0.y != 0) ? 1065353216 : 0;
	r0.x = asint(asfloat(r0.y) * asfloat(r0.x));
	r2 = albedoMap.Sample(linearSampler, i.texcoord.xy);
	r0.xyw = asint(r2.xyz * asfloat(r0.www) + asfloat(r0.xxx));
	o.w = r2.w;
	r1.xyz = -(asfloat(r0.xyw)) + fogColor.xyz;
	r2.x = -(fogRange.x) + fogRange.y;
	r1.w = saturate(r1.w / r2.x);
	r1.xyz = r1.www * r1.xyz + asfloat(r0.xyw);
	o.xyz = (r0.zzz != 0) ? r1.xyz : asfloat(r0.xyw);

	return o;
}
