float4 lightColor : register(c0);
float3x3 basis;
float2 scales[2];
float4 tint;
float4 fade;
float4 rim : register(c8);
sampler2D albedo;
sampler2D mask : register(s3);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0.x = dot(i.texcoord1.xyz, transpose(basis)[0].xyz);
	r0.y = dot(i.texcoord1.xyz, transpose(basis)[1].xyz);
	r0.z = dot(i.texcoord1.xyz, transpose(basis)[2].xyz);
	r0.x = saturate(dot(r0.xyz, lightColor.xyz));
	r0.x = r0.x * lightColor.w;
	r1.xy = scales[0].xy;
	r0.yw = i.texcoord.xy * r1.xy + scales[1].xy;
	r1 = tex2D(albedo, r0.yw);
	r1 = r1 * tint;
	r1.xyz = r0.xxx * r1.xyz;
	r2 = tex2D(mask, i.texcoord.xy);
	r3 = lerp(r1, fade, r2.x);
	o = rim * r0.z + r3;

	return o;
}
