sampler2D s0;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float3 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r8;
	float3 r9;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, i.texcoord.xy);
	r9.x = dot(i.texcoord1.xyz, r8.xyz);
	r9.y = dot(i.texcoord2.xyz, r8.xyz);
	r9.z = dot(i.texcoord3.xyz, r8.xyz);
	r11.xyz = r9.xyz;
	r11.w = 1;
	r0 = r11 * i.color;
	o = r0;

	return o;
}
