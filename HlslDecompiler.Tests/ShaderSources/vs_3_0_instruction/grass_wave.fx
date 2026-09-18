float4 wind : register(c4);
float4x4 worldViewProjection;

struct VS_IN
{
	float4 position : POSITION;
	float4 texcoord : TEXCOORD;
	float4 texcoord1 : TEXCOORD1;
};

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float fog : FOG;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	float4 r0;
	float3 r1;
	float r2;
	r0.x = wind.w + i.texcoord1.x;
	r0.xy = r0.xx * float2(0.15915494, 0.07957747) + 0.5;
	r0.xy = frac(r0.xy);
	r0.xy = r0.xy * 6.2831855 + -3.1415927;
	r1.y = sin(r0.x);
	r2.x = cos(r0.y);
	r0.x = r1.y * r2.x;
	r0.x = r0.x * wind.z;
	r0.x = r0.x * i.texcoord.y;
	r1.xz = i.position.xz;
	r0.yz = r1.xz + i.texcoord1.yz;
	r0.xz = wind.xy * r0.xx + r0.yz;
	r0.yw = i.position.yw;
	o.position.x = dot(r0, transpose(worldViewProjection)[0]);
	o.position.y = dot(r0, transpose(worldViewProjection)[1]);
	o.position.z = dot(r0, transpose(worldViewProjection)[2]);
	o.position.w = dot(r0, transpose(worldViewProjection)[3]);
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = 1 / sqrt(r0.x);
	r0.x = 1 / r0.x;
	o.fog = saturate(r0.x * -i.texcoord1.w + 1);
	o.texcoord = i.texcoord.xy;

	return o;
}
