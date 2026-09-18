sampler2D baseMap;
float4 blendControl;
sampler2D blendMap : register(s2);
sampler2D detailMap;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	r0.xy = texcoord.xy * blendControl.xx;
	r0 = tex2D(detailMap, r0.xy);
	r1 = tex2D(baseMap, texcoord.xy);
	r2 = tex2D(blendMap, texcoord.xy);
	r0.xyz = r0.xyz * r1.xyz;
	r0.xyz = r0.xyz * 2 + -r1.xyz;
	r0.xyz = r2.xxx * r0.xyz + r1.xyz;
	r0.w = -r2.x + blendControl.y;
	r1.xyz = (r0.www >= 0) ? r1.xyz : r0.xyz;
	o = r1;

	return o;
}
