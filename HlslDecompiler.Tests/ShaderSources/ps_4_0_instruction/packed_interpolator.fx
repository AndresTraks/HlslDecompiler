float4 scale;
float3 sunDir;
float fogStart;
float fogEnd;
float3 fogColour;

SamplerState samp;
Texture2D layer0;
Texture2D layer1;
Texture2D layer2;
Texture2D blendMap;

struct PS_IN
{
	float3 normal : NORMAL;
	float3 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0 = blendMap.Sample(samp, i.texcoord.xy);
	r0.w = r0.y + r0.x;
	r0.w = r0.z + r0.w;
	r0.w = max(r0.w, 0.0000999999975);
	r0.xyz = r0.xyz / r0.www;
	r1 = i.texcoord.xyxy * scale.xxyy;
	r2 = layer1.Sample(samp, r1.zw);
	r1 = layer0.Sample(samp, r1.xy);
	r2.xyz = r0.yyy * r2.xyz;
	r0.xyw = r1.xyz * r0.xxx + r2.xyz;
	r1.xy = i.texcoord.xy * scale.zz;
	r1 = layer2.Sample(samp, r1.xy);
	r0.xyz = r1.xyz * r0.zzz + r0.xyw;
	r0.w = dot(i.normal.xyz, i.normal.xyz);
	r0.w = 1 / sqrt(r0.w);
	r1.xyz = r0.www * i.normal.xyz;
	r0.w = saturate(dot(r1.xyz, -(sunDir.xyz)));
	r0.w = r0.w * 0.800000012 + 0.200000003;
	r1.xyz = r0.www * r0.xyz;
	r0.xyz = -(r0.xyz) * r0.www + fogColour.xyz;
	r0.w = i.texcoord.z + -(fogStart);
	r1.w = -(fogStart) + fogEnd;
	r0.w = saturate(r0.w / r1.w);
	o.xyz = r0.www * r0.xyz + r1.xyz;
	o.w = 1;

	return o;
}
