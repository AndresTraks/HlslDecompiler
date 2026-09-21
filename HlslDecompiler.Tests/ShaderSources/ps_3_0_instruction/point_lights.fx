float3 cameraPosition : register(c8);
sampler2D diffuseMap;
samplerCUBE envMap : register(s2);
float3 lightColors[4] : register(c4);
int lightCount;
float3 lightPositions[4];
sampler2D normalMap;
float specularPower : register(c9);
float2 uvScale : register(c10);

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float2 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	float4 r4;
	float4 r5;
	float3 r6;
	float3 r7;
	r0.xy = uvScale.xy * i.texcoord3.xy;
	r1 = tex2D(diffuseMap, r0.xy);
	r1 = r1 * i.color;
	r2.xyz = normalize(i.texcoord1.xyz);
	r0.z = dot(i.texcoord2.xyz, r2.xyz);
	r3.xyz = r2.xyz * -r0.zzz + i.texcoord2.xyz;
	r4.xyz = normalize(r3.xyz);
	r3.xyz = r2.zxy * r4.yzx;
	r3.xyz = r2.yzx * r4.zxy + -r3.xyz;
	r0 = tex2D(normalMap, r0.xy);
	r0.xyz = r0.xyz * 2 + -1;
	r3.xyz = r3.xyz * r0.yyy;
	r0.xyw = r0.xxx * r4.xyz + r3.xyz;
	r0.xyz = r0.zzz * r2.xyz + r0.xyw;
	r2.xyz = normalize(r0.xyz);
	r0.xyz = cameraPosition.xyz + -i.texcoord.xyz;
	r3.xyz = normalize(r0.xyz);
	r0 = float4(0.1, 0.1, 0.1, 0);
	for (int i0 = 0; i0 < lightCount; i0++) {
		r4 = r0.w + float4(-0, -1, -2, -3);
		r2.w = 0;
		r5.xyz = (-abs(r4.xxx) >= 0) ? lightPositions[0].xyz : r2.www;
		r5.xyz = (-abs(r4.yyy) >= 0) ? lightPositions[1].xyz : r5.xyz;
		r5.xyz = (-abs(r4.zzz) >= 0) ? lightPositions[2].xyz : r5.xyz;
		r5.xyz = (-abs(r4.www) >= 0) ? lightPositions[3].xyz : r5.xyz;
		r5.xyz = r5.xyz + -i.texcoord.xyz;
		r3.w = dot(r5.xyz, r5.xyz);
		r5.w = rsqrt(r3.w);
		r6 = r5.www * r5.xyz;
		r6.x = saturate(dot(r2.xyz, r6.xyz));
		r5.xyz = r5.xyz * r5.www + r3.xyz;
		r7 = normalize(r5.xyz);
		r5.x = saturate(dot(r2.xyz, r7.xyz));
		r6.y = pow(r5.x, specularPower.x);
		r5.xyz = (-abs(r4.xxx) >= 0) ? lightColors[0].xyz : r2.www;
		r5.xyz = (-abs(r4.yyy) >= 0) ? lightColors[1].xyz : r5.xyz;
		r4.xyz = (-abs(r4.zzz) >= 0) ? lightColors[2].xyz : r5.xyz;
		r4.xyz = (-abs(r4.www) >= 0) ? lightColors[3].xyz : r4.xyz;
		r2.w = r6.y + r6.x;
		r4.xyz = r2.www * r4.xyz;
		r2.w = r3.w + 1;
		r2.w = 1 / r2.w;
		r0.xyz = r4.xyz * r2.www + r0.xyz;
		r0.w = r0.w + 1;
	}
	r0.w = dot(-r3.xyz, r2.xyz);
	r0.w = r0.w + r0.w;
	r4.xyz = r2.xyz * -r0.www + -r3.xyz;
	r4 = texCUBE(envMap, r4.xyz);
	r0.w = saturate(dot(r2.xyz, r3.xyz));
	r0.w = -r0.w + 1;
	r0.w = r0.w * r0.w;
	r0.w = r0.w * r0.w;
	r2.xyz = r0.www * r4.xyz;
	o.xyz = r1.xyz * r0.xyz + r2.xyz;
	o.w = r1.w;

	return o;
}
